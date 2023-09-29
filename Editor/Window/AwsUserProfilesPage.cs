// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Editor.CoreAPI;
using UnityEngine;
using UnityEngine.UIElements;

namespace AmazonGameLift.Editor
{
    internal class AwsUserProfilesPage
    {
        public List<TextField> AccountDetailTextFields = new();
        
        public readonly AwsCredentialsUpdate UpdateModel;
        public readonly AwsCredentialsCreation CreationModel;
        public readonly BootstrapSettings BootstrapSettings;
        
        private VisualElement _currentElement;

        private const string hiddenClassName = "hidden";
        private readonly StateManager _stateManager;
        private readonly VisualElement _container;
        private readonly UserProfileCreation _userProfileCreation;

        public AwsUserProfilesPage(VisualElement container, StateManager stateManager)
        {
            var awsCredentials = new AwsCredentialsFactory().Create();
            CreationModel = awsCredentials.Creation;
            UpdateModel = awsCredentials.Update;
            
            _container = container;
            var mVisualTreeAsset = Resources.Load<VisualTreeAsset>("EditorWindow/Pages/AwsUserProfilesPage");
            var uxml = mVisualTreeAsset.Instantiate();

            _container.Add(uxml);
            LocalizeText();

            _stateManager = stateManager;
            CreationModel.OnCreated += () => _stateManager.SelectedProfileName = CreationModel.ProfileName;
            _stateManager.OnProfileSelected += UpdateModel.Update;
            
            _userProfileCreation = new UserProfileCreation(_container, _stateManager, this);
            BootstrapSettings = _userProfileCreation.SetupBootstrap();
                
            SetupConfigSettings();
            RefreshProfiles();

            container.Q<DropdownField>("UserProfilePageAccountNewProfileRegionDropdown").choices =
                _stateManager.CoreApi.ListAvailableRegions().ToList();
            
            SetupTab();
            SetupButtonCallbacks();
        }
        
        private void LocalizeText()
        {
            var l = new ElementLocalizer(_container);
            l.SetElementText("UserProfilePageAccountCardNewAccountTitle", Strings.UserProfilePageAccountCardNewAccountTitle);
            l.SetElementText("UserProfilePageAccountCardNewAccountDescription", Strings.UserProfilePageAccountCardNewAccountDescription);
            l.SetElementText("UserProfilePageAccountCardNoAccountTitle", Strings.UserProfilePageAccountCardNoAccountTitle);
            l.SetElementText("UserProfilePageAccountCardNoAccountDescription", Strings.UserProfilePageAccountCardNoAccountDescription);
            l.SetElementText("UserProfilePageAccountCardNewAccountTitle", Strings.UserProfilePageAccountCardNewAccountTitle);
            l.SetElementText("UserProfilePageAccountCardNewAccountDescription", Strings.UserProfilePageAccountCardNewAccountDescription);
            l.SetElementText("UserProfilePageAccountNewProfileTitle", Strings.UserProfilePageAccountNewProfileTitle);
            l.SetElementText("UserProfilePageAccountNewProfileName", Strings.UserProfilePageAccountNewProfileName);
            l.SetElementText("UserProfilePageAccountNewProfileAccessKeyInput", Strings.UserProfilePageAccountNewProfileAccessKeyInput);
            l.SetElementText("UserProfilePageAccountNewProfileSecretKeyInput", Strings.UserProfilePageAccountNewProfileSecretKeyInput);
            l.SetElementText("UserProfilePageAccountNewProfileRegion", Strings.UserProfilePageAccountNewProfileRegion);
            l.SetElementText("UserProfilePageAccountNewProfileRegionPlaceholderDropdown", Strings.UserProfilePageAccountNewProfileRegionPlaceholderDropdown);
            l.SetElementText("UserProfilePageAccountCardNoAccountLink", Strings.UserProfilePageAccountCardNoAccountLink);
            l.SetElementText("UserProfilePageAccountNewProfileHelpLink", Strings.UserProfilePageAccountNewProfileHelpLink);
            l.SetElementText("UserProfilePageAccountCardNoAccountButtonLabel", Strings.UserProfilePageAccountCardNoAccountButtonLabel);
            l.SetElementText("UserProfilePageAccountCardHasAccountButton", Strings.UserProfilePageAccountCardHasAccountButton);
            l.SetElementText("UserProfilePageAccountNewProfileCreateButton", Strings.UserProfilePageAccountNewProfileCreateButton);
            l.SetElementText("UserProfilePageAccountNewProfileCancelButton", Strings.UserProfilePageAccountNewProfileCancelButton);
            l.SetElementText("UserProfilePageBootstrapTitle", Strings.UserProfilePageBootstrapTitle);
            l.SetElementText("UserProfilePageBootstrapDescription", Strings.UserProfilePageBootstrapDescription);
            l.SetElementText("UserProfilePageBootstrapPricingText", Strings.UserProfilePageBootstrapPricingText);
            l.SetElementText("UserProfilePageBootstrapProfileInputText", Strings.UserProfilePageBootstrapProfileInputText);
            l.SetElementText("UserProfilePageBootstrapBucketText", Strings.UserProfilePageBootstrapBucketText);
            l.SetElementText("UserProfilePageBootstrapBucketUnsetText", Strings.UserProfilePageBootstrapBucketUnsetText);
            l.SetElementText("LabelBootstrapRegion", Strings.LabelBootstrapRegion);
            l.SetElementText("UserProfilePageBootstrapStatusText", Strings.UserProfilePageBootstrapStatusText);
            l.SetElementText("UserProfilePageBootstrapWarningText", Strings.UserProfilePageBootstrapWarningText);
            l.SetElementText("UserProfilePageBootstrapProfilePlaceholderText", Strings.UserProfilePageBootstrapProfilePlaceholderText);
            l.SetElementText("UserProfilePageBootstrapPricingInfoText", Strings.UserProfilePageBootstrapPricingInfoText);
            l.SetElementText("UserProfilePageBootstrapPricingFreeTierText", Strings.UserProfilePageBootstrapPricingFreeTierText);
            l.SetElementText("UserProfilePageBootstrapHelpLink", Strings.UserProfilePageBootstrapHelpLink);
            l.SetElementText("UserProfilePageBootstrapStartButton", Strings.UserProfilePageBootstrapStartButton);
            l.SetElementText("UserProfilePageBootstrapAnotherProfileButton", Strings.UserProfilePageBootstrapAnotherProfileButton);
            l.SetElementText("UserProfilePageBootstrapAnotherBucketButton", Strings.UserProfilePageBootstrapAnotherBucketButton);
        }

        private void SetupButtonCallbacks()
        {
            _container.Q<Button>("UserProfilePageAccountCardNoAccountButton").RegisterCallback<ClickEvent>(_ => OpenLink(""));
            _container.Q<Button>("UserProfilePageBootstrapAnotherProfileButton").RegisterCallback<ClickEvent>(_ =>
            {
                var targetWizard = _container.Q<VisualElement>("AddNewProfile");
                ChangeWizard(targetWizard);
            });
            _container.Q<Button>("UserProfilePageAccountNewProfileCreateButton").RegisterCallback<ClickEvent>(_ =>
            {
                if (SaveProfile())
                {
                    var targetWizard = _container.Q<VisualElement>("BootstrapMenu");
                    ChangeWizard(targetWizard);
                }
                else
                {
                    Debug.Log("Error");
                }
            });
            _container.Q<Button>("UserProfilePageAccountNewProfileCancelButton").RegisterCallback<ClickEvent>(_ =>
            {
                ClearCredentials();
                SetupBootMenu();
            });
            _container.Q<Button>("UserProfilePageBootstrapStartButton").RegisterCallback<ClickEvent>(_ =>
            {
                BootstrapSettings.RefreshBucketName();
                OpenS3Popup(BootstrapSettings.BucketName);
            });
            _container.Q<Button>("UserProfilePageBootstrapAnotherBucketButton").RegisterCallback<ClickEvent>(_ =>
            {
                OpenS3Popup(_stateManager.BucketName);
            });
            _container.Q<Button>("AccessKeyToggleReveal").RegisterCallback<ClickEvent>(_ =>
            {
                var accessToggle = _container.Q<TextField>("AccessKeyField");
                ToggleHiddenText(accessToggle);
            });
            _container.Q<Button>("SecretKeyToggleReveal").RegisterCallback<ClickEvent>(_ =>
            {
                var secretToggle = _container.Q<TextField>("SecretKeyField");
                ToggleHiddenText(secretToggle);
            });
            _container.Q<Button>("AddProfile").RegisterCallback<ClickEvent>(_ =>
            {
                var targetWizard = _container.Q<VisualElement>("AddNewProfile");
                ChangeWizard(targetWizard);
            });
        }

        private void OpenLink(string url)
        {
            Application.OpenURL(url);
        }

        private void SetupTab()
        {
            SetupBootMenu();
        }

        private void SetupConfigSettings()
        {
            // var selectedProfile = _stateManager.CoreApi.GetSetting(SettingsKeys.CurrentProfileName);
            // _stateManager.SelectedProfileName = selectedProfile.Success ? selectedProfile.Value : _stateManager.AllProfiles.First();
        }

        private void SetupBootMenu()
        {
            VisualElement targetWizard;
            // var tab2WarningBox = _container.Q<VisualElement>(null, "Tab2Warning");
            var cardsMenu =  _container.Q<VisualElement>("Cards");
            var newProfileMenu = _container.Q<VisualElement>("AddNewProfile");
            var bootStrapMenu = _container.Q<VisualElement>("BootstrapMenu");
            var completedMenu = _container.Q<VisualElement>("CompletedProfile");
            cardsMenu.AddToClassList(hiddenClassName);
            newProfileMenu.AddToClassList(hiddenClassName);
            bootStrapMenu.AddToClassList(hiddenClassName);
            completedMenu.AddToClassList(hiddenClassName);
            AccountDetailTextFields = newProfileMenu.Query<TextField>().ToList();
            switch (_stateManager.AllProfiles.Count)
            {
                case 0:
                    targetWizard = _container.Q<VisualElement>("Cards");
                    break;
                case 1:
                    if (_stateManager.SelectedProfile == null)
                    {
                        // tab2WarningBox.style.display = DisplayStyle.Flex;
                        //TODO Set SelectedProfile and change dropdown to "Choose Profile" and make all the below labels ---
                    }

                    targetWizard = _stateManager.IsBootstrapped == false
                        ? bootStrapMenu
                        : completedMenu;
                    break;
                default:
                {
                    if (_stateManager.AllProfiles.Any(profile => profile == "default"))
                    {
                        targetWizard = _stateManager.IsBootstrapped == false
                            ? bootStrapMenu
                            : completedMenu;
                    }
                    else
                    {
                        targetWizard = bootStrapMenu;
                        if (_stateManager.SelectedProfile == null)
                        {
                            // tab2WarningBox.style.display = DisplayStyle.Flex;
                        }
                    }

                    break;
                }
            }

            ChangeWizard(targetWizard);
        }

        private bool SaveProfile()
        {
            if (!_userProfileCreation.CreateUserProfile())
            {
                return false;
            }
            Debug.Log("Saving Profile");
            return true;
        }

        private void ClearCredentials()
        {
            AccountDetailTextFields = _container.Query<TextField>(null, "AccountDetailsInput").ToList();
            foreach (var textField in AccountDetailTextFields)
            {
                textField.value = "";
            }
        }

        private void ToggleHiddenText(TextField hiddenField)
        {
            hiddenField.isPasswordField = !hiddenField.isPasswordField;
        }
        
        private void ChangeWizard(VisualElement targetWizard)
        {
            if (_currentElement != null)
            {
                _currentElement.AddToClassList(hiddenClassName);
            }

            _currentElement = targetWizard;
            if (_currentElement != null)
            {
                _currentElement.RemoveFromClassList(hiddenClassName);
            }
        }
        
        public void RefreshProfiles()
        {
            UpdateModel.Refresh();
        }
        
        private void BootstrapAccount(string bucketName)
        { 
            _userProfileCreation.BootstrapAccount(bucketName);
        }
        
        public void OpenS3Popup(string bucketName)
        {
            var popup = ScriptableObject.CreateInstance<GameLiftPluginBucketPopup>();
            popup.Init(bucketName);
            popup.OnConfirm += BootstrapAccount;
            popup.ShowModalUtility();
        }
    }
}