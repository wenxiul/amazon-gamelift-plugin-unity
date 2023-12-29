﻿// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;
using OperatingSystem = Amazon.GameLift.OperatingSystem;
using AmazonGameLift.Runtime;

namespace AmazonGameLift.Editor
{
    public class ManagedEC2Page
    {
        private const string _primaryButtonClassName = "button--primary";
        private const string _hiddenClassName = "hidden";
        private const int RefreshUIMilliseconds = 2000;
        private readonly VisualElement _container;
        private readonly StateManager _stateManager;
        private readonly DeploymentSettings _deploymentSettings;
        private readonly Button _deployButton;
        private readonly Button _deleteButton;
        private readonly Button _launchClientButton;
        private readonly VisualElement _launchClientDescription;
        private readonly Button _configureClientButton;
        private readonly VisualElement _statusLink;
        private readonly DeploymentScenariosInput _deploymentScenariosInput;
        private readonly FleetParametersInput _fleetParamsInput;
        private readonly StatusIndicator _statusIndicator;
        private readonly ManagedEC2Deployment _ec2Deployment;
        private GameLiftClientSettings _gameLiftClientSettings;
        private GameLiftClientSettingsLoader _gameLiftClientSettingsLoader;
        private StatusBox _bootstrapStatusBox;
        private StatusBox _deployStatusBox;
        private StatusBox _launchStatusBox;

        public ManagedEC2Page(VisualElement container, StateManager stateManager)
        {
            _container = container;
            _stateManager = stateManager;
            _deploymentSettings = DeploymentSettingsFactory.Create(stateManager);
            if (_stateManager.IsBootstrapped)
            {
                _deploymentSettings.Restore();
            }
            _deploymentSettings.Refresh();
            var parameters = GetManagedEC2Parameters(_deploymentSettings);

            var mVisualTreeAsset = Resources.Load<VisualTreeAsset>("EditorWindow/Pages/ManagedEC2Page");
            var uxml = mVisualTreeAsset.Instantiate();

            container.Add(uxml);
            SetupStatusBoxes();

            _stateManager.OnUserProfileUpdated += UpdateStatusBoxes;
            _ec2Deployment = new ManagedEC2Deployment(_deploymentSettings);
            var scenarioContainer = container.Q("ManagedEC2ScenarioTitle");
            _deploymentScenariosInput =
                new DeploymentScenariosInput(scenarioContainer, _deploymentSettings.Scenario,
                    _stateManager.IsBootstrapped, stateManager);
            _deploymentScenariosInput.OnValueChanged += value => { _deploymentSettings.Scenario = value; };
            _statusIndicator = _container.Q<StatusIndicator>();
            var parametersContainer = container.Q<Foldout>("ManagedEC2ParametersTitle");
            _fleetParamsInput = new FleetParametersInput(parametersContainer, parameters);
            _fleetParamsInput.OnValueChanged += fleetParameters =>
            {
                _ec2Deployment.UpdateModelFromParameters(fleetParameters);
                UpdateGUI();
            };

            _stateManager.OnUserProfileUpdated += UpdateDeploymentSettings;
            _stateManager.OnClientSettingsChanged += UpdateGUI;

            _deployButton = container.Q<Button>("ManagedEC2CreateStackButton");
            _deployButton.RegisterCallback<ClickEvent>(_ =>
            {
                _ec2Deployment.StartDeployment();
                UpdateGUI();
            });
                        
            _deleteButton = container.Q<Button>("ManagedEC2DeleteStackButton");
            _deleteButton.RegisterCallback<ClickEvent>(async _ =>
            {
                await _ec2Deployment.DeleteDeployment();
                _deploymentSettings.RefreshCurrentStackInfo();
                UpdateGUI();
            });
            
            _launchClientButton = container.Q<Button>("ManagedEC2LaunchClientButton");
            _launchClientButton.RegisterCallback<ClickEvent>(_ =>
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone,
                    EditorUserBuildSettings.selectedStandaloneTarget);
                EditorApplication.EnterPlaymode();
            });

            _launchClientDescription = container.Q<VisualElement>("ManagedEC2LaunchClientDescription");

            LoadGameLiftClientSettings();
            _configureClientButton = container.Q<Button>("ManagedEC2ConfigureClientButton");
            _configureClientButton.RegisterCallback<ClickEvent>(_ =>
            {
                _gameLiftClientSettings.ConfigureManagedEC2ClientSettings(_stateManager.Region, _deploymentSettings.CurrentStackInfo.ApiGatewayEndpoint, _deploymentSettings.CurrentStackInfo.UserPoolClientId);
                _stateManager.OnClientSettingsChanged?.Invoke();
            });
            
            _statusLink = container.Q("ManagedEC2DeployStatusLink");
            _statusLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(
                string.Format(Urls.AwsCloudFormationEventsTemplate, _stateManager.Region, _deploymentSettings.CurrentStackInfo.StackId)));

            _container.Q<VisualElement>("ManagedEC2IntegrateServerLinkParent")
                .RegisterCallback<ClickEvent>(_ => Application.OpenURL(Urls.ManagedEC2IntegrateServerLink));
            
            _container.Q<VisualElement>("ManagedEC2IntegrateClientLinkParent")
                .RegisterCallback<ClickEvent>(_ => Application.OpenURL(Urls.ManagedEC2IntegrateClientLink));

            _deploymentSettings.CurrentStackInfoChanged += UpdateGUI;
            _deploymentSettings.Scenario = _stateManager.DeploymentScenario;    
            
            UpdateGUI();
            UpdateStatusBoxes();
        }

        private ManagedEC2FleetParameters GetManagedEC2Parameters(DeploymentSettings deploymentSettings)
        {
            return new ManagedEC2FleetParameters
            {
                GameName = deploymentSettings.GameName ?? Application.productName,
                FleetName = deploymentSettings.FleetName ?? $"{Application.productName}-ManagedFleet",
                LaunchParameters = deploymentSettings.LaunchParameters ?? $"",
                BuildName = deploymentSettings.BuildName ??
                            $"{Application.productName}-{deploymentSettings.ScenarioName.Replace(" ", "_")}-Build",
                GameServerFile = deploymentSettings.BuildFilePath,
                GameServerFolder = deploymentSettings.BuildFolderPath,
                OperatingSystem = OperatingSystem.FindValue(deploymentSettings.BuildOperatingSystem) ??
                                  OperatingSystem.AMAZON_LINUX_2
            };           
        }

        private void UpdateDeploymentSettings()
        {
            if (_stateManager.IsBootstrapped)
            {
                _deploymentSettings.Refresh();
                _deploymentSettings.Restore();
                _ec2Deployment.UpdateModelFromParameters(GetManagedEC2Parameters(_deploymentSettings));
            }

            UpdateGUI();
        }

        private void LoadGameLiftClientSettings()
        {
            _gameLiftClientSettings = _gameLiftClientSettingsLoader.LoadAsset();
            _container.schedule.Execute(() => {
                LoadGameLiftClientSettings();
                UpdateGUI();
            }).StartingIn(RefreshUIMilliseconds);
        }

        private void UpdateGUI()
        {
            LocalizeText();

            bool canDeploy = _deploymentSettings.CurrentStackInfo.StackStatus == null &&
                                     _deploymentSettings.CanDeploy;

            _deployButton.SetEnabled(canDeploy);
            if(canDeploy) 
            {
                _deployButton.AddToClassList(_primaryButtonClassName);
            }
            else
            {
                _deployButton.RemoveFromClassList(_primaryButtonClassName);
            }

            _deleteButton.SetEnabled(_deploymentSettings.CanDelete);

            bool canLaunchClient = _deploymentSettings.CurrentStackInfo.StackStatus is StackStatus.CreateComplete or StackStatus.UpdateComplete;

            // if the client settings have changed due to a deployment or due to manual changes, this will require the user to configure the client settings again
            bool isClientConfigured = _gameLiftClientSettings && !_gameLiftClientSettings.IsGameLiftAnywhere
                                            && _gameLiftClientSettings.AwsRegion == _stateManager.Region
                                            && _gameLiftClientSettings.ApiGatewayUrl == _deploymentSettings.CurrentStackInfo.ApiGatewayEndpoint
                                            && _gameLiftClientSettings.UserPoolClientId == _deploymentSettings.CurrentStackInfo.UserPoolClientId;

            bool isLaunchClientEnabled = canLaunchClient && isClientConfigured;
            bool isConfigureClientEnabled = canLaunchClient && !isClientConfigured && _gameLiftClientSettings;

            _launchClientButton.SetEnabled(isLaunchClientEnabled);
            if (isLaunchClientEnabled)
            {
                _launchClientButton.AddToClassList(_primaryButtonClassName);
            }
            else
            {
                _launchClientButton.RemoveFromClassList(_primaryButtonClassName);
            }

            if (_deploymentSettings.Scenario == DeploymentScenarios.FlexMatch)
            {
                _launchClientButton.AddToClassList(_hiddenClassName);
                _launchClientDescription.RemoveFromClassList(_hiddenClassName);
            }
            else
            {
                _launchClientButton.RemoveFromClassList(_hiddenClassName);
                _launchClientDescription.AddToClassList(_hiddenClassName);
            }

            _configureClientButton.SetEnabled(isConfigureClientEnabled);
            if(isConfigureClientEnabled) 
            {
                _configureClientButton.AddToClassList(_primaryButtonClassName);   
            }
            else
            {
                _configureClientButton.RemoveFromClassList(_primaryButtonClassName);   
            }

            _deploymentScenariosInput.SetEnabled(_deploymentSettings.CanEdit);
            _fleetParamsInput.SetEnabled(_deploymentSettings.CanEdit);

            _deployStatusBox.Close();
            var stackStatus = _deploymentSettings.CurrentStackInfo.StackStatus;
            var textProvider = new TextProvider();
            if (stackStatus == null)
            {
                _statusIndicator.Set(State.Inactive, textProvider.Get(Strings.ManagedEC2DeployStatusNotDeployed));
            }
            else if (stackStatus.IsStackStatusFailed())
            {
                _statusIndicator.Set(State.Failed, textProvider.Get(Strings.ManagedEC2DeployStatusFailed));
                _deployStatusBox.Show(StatusBox.StatusBoxType.Error, textProvider.GetError(ErrorCode.StackStatusInvalid));
            }
            else if (stackStatus == StackStatus.DeleteInProgress)
            {
                _statusIndicator.Set(State.InProgress, textProvider.Get(Strings.ManagedEC2DeployStatusDeleting));
            }
            else if (stackStatus.IsStackStatusRollback())
            {
                _statusIndicator.Set(State.Failed, textProvider.Get(stackStatus.IsStackStatusInProgress()
                    ? Strings.ManagedEC2DeployStatusRollingBack
                    : Strings.ManagedEC2DeployStatusRolledBack));
                _deployStatusBox.Show(StatusBox.StatusBoxType.Error,
                    textProvider.GetError(ErrorCode.StackStatusInvalid));
            }
            else if (stackStatus.IsStackStatusInProgress())
            {
                _statusIndicator.Set(State.InProgress, textProvider.Get(Strings.ManagedEC2DeployStatusDeploying));
            }
            else if (stackStatus.IsStackStatusOperationDone())
            {
                _statusIndicator.Set(State.Success, textProvider.Get(Strings.ManagedEC2DeployStatusDeployed));
            }
            else
            {
                _statusIndicator.Set(State.Inactive, textProvider.Get(Strings.ManagedEC2DeployStatusNotDeployed));
            }

            _statusLink.visible = _deploymentSettings.HasCurrentStack;
        }
        
        private void SetupStatusBoxes()
        {
            _bootstrapStatusBox = _container.Q<StatusBox>("ManagedEC2StatusBox");
            _deployStatusBox = _container.Q<StatusBox>("ManagedEC2DeployStatusBox");
            _launchStatusBox = _container.Q<StatusBox>("ManagedEC2LaunchStatusBox");
            _gameLiftClientSettingsLoader = new GameLiftClientSettingsLoader(_launchStatusBox);
        }
        
        private void UpdateStatusBoxes()
        {
            if (!_stateManager.IsBootstrapped)
            {
                _bootstrapStatusBox.Show(StatusBox.StatusBoxType.Warning, Strings.ManagedEC2StatusBoxNotBootstrappedWarning);
            }
            else
            {
                _bootstrapStatusBox.Close();
            }
        }

        private void LocalizeText()
        {
            var l = new ElementLocalizer(_container);
            var replacements = new Dictionary<string, string>()
            {
                { "GameName", Application.productName },
                { "ScenarioType", GetScenarioType(l) }
            };
            l.SetElementText("ManagedEC2Title", Strings.ManagedEC2Title);
            l.SetElementText("ManagedEC2Description", Strings.ManagedEC2Description);
            l.SetElementText("ManagedEC2IntegrateTitle", Strings.ManagedEC2IntegrateTitle);
            l.SetElementText("ManagedEC2IntegrateDescription", Strings.ManagedEC2IntegrateDescription);
            l.SetElementText("ManagedEC2IntegrateServerLink", Strings.ManagedEC2IntegrateServerLink);
            l.SetElementText("ManagedEC2IntegrateClientLink", Strings.ManagedEC2IntegrateClientLink);
            l.SetElementText("ManagedEC2ScenarioTitle", Strings.ManagedEC2ScenarioTitle);
            l.SetElementText("ManagedEC2ParametersTitle", Strings.ManagedEC2ParametersTitle, replacements);
            l.SetElementText("ManagedEC2DeployTitle", Strings.ManagedEC2DeployTitle, replacements);
            l.SetElementText("ManagedEC2DeployDescription", Strings.ManagedEC2DeployDescription);
            l.SetElementText("ManagedEC2DeployStatusLabel", Strings.ManagedEC2DeployStatusLabel);
            l.SetElementText("ManagedEC2DeployActionsLabel", Strings.ManagedEC2DeployActionsLabel);
            l.SetElementText("ManagedEC2CreateStackButton", Strings.ManagedEC2CreateStackButton);
            l.SetElementText("ManagedEC2DeleteStackButton", Strings.ManagedEC2DeleteStackButton);
            l.SetElementText("ManagedEC2LaunchClientTitle", Strings.ManagedEC2LaunchClientTitle);
            l.SetElementText("ManagedEC2LaunchClientLabel", Strings.ManagedEC2LaunchClientLabel);
            l.SetElementText("ManagedEC2LaunchClientButton", Strings.ManagedEC2LaunchClientButton);
            l.SetElementText("ManagedEC2LaunchClientDescription", Strings.ManagedEC2LaunchClientDescription);
            l.SetElementText("ManagedEC2ConfigureClientLabel", Strings.ManagedEC2ConfigureClientLabel);
            l.SetElementText("ManagedEC2ConfigureClientButton", Strings.ManagedEC2ConfigureClientButton);
            l.SetElementText("ManagedEC2DeployStatusLinkLabel", Strings.ManagedEC2DeployStatusLink);
        }

        private string GetScenarioType(ElementLocalizer l) => _deploymentSettings.Scenario switch
        {
            DeploymentScenarios.SingleRegion => l.GetText(Strings.ManagedEC2ScenarioSingleFleetLabel),
            DeploymentScenarios.FlexMatch => l.GetText(Strings.ManagedEC2ScenarioFlexMatchLabel),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
