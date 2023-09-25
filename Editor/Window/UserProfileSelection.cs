﻿// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Threading;
using Editor.CoreAPI;
using UnityEngine.UIElements;

namespace AmazonGameLift.Editor
{
    internal class UserProfileSelection
    {
        private BootstrapSettings _bootstrapSettings;
        private CancellationTokenSource _refreshBucketsCancellation;
        private readonly VisualElement _container;
        private readonly AwsUserProfilesPage _awsUserProfilesPage;
        private readonly StateManager _stateManager;
        
        public UserProfileSelection(VisualElement container, StateManager stateManager, AwsUserProfilesPage profilesPage)
        {
            _container = container;
            _stateManager = stateManager;
            _awsUserProfilesPage = profilesPage;
            _bootstrapSettings = profilesPage.BootstrapSettings;
        }

        private void BucketSelection()
        {
            _refreshBucketsCancellation = new CancellationTokenSource();
            _ = _bootstrapSettings.RefreshExistingBuckets(_refreshBucketsCancellation.Token);
            if (_bootstrapSettings.BucketName != null)
            {
                BucketSelection(_bootstrapSettings.BucketName);
                _bootstrapSettings.SaveSelectedBucket();
            }
        }

        public void BucketSelection(string selectedBucket)
        {
            if (_bootstrapSettings.SelectBucket(selectedBucket))
            {
                _bootstrapSettings.SaveSelectedBucket();
                _container.Q<Label>("S3BucketNameLabel").text = selectedBucket;
            }
        }

        public void AccountSelection(bool isSetup)
        {
            _awsUserProfilesPage.RefreshProfiles();
            var accountSelectFields = _container.Query<DropdownField>("AccountSelection").ToList();
            foreach (var accountSelect in accountSelectFields)
            {
                if (isSetup)
                {
                    accountSelect.RegisterValueChangedCallback(_ => { OnAccountSelect(accountSelect.index); });
                }
                
                accountSelect.choices = _awsUserProfilesPage.UpdateModel.AllProlfileNames.ToList();
                if (accountSelect.choices.Contains("default"))
                {
                    accountSelect.index = accountSelect.choices.IndexOf(_stateManager.SelectedProfileName is "default" or null ? "default" : _stateManager.SelectedProfileName);
                }
            }
        }

        private void OnAccountSelect(int index)
        {
            UpdateModel(index);
            BucketSelection(_stateManager.SelectedProfile.BootStrappedBucket);
            
            var accountSelectLabels = _container.Query<Label>(null, "AccountSelectLabel").ToList();
            foreach (var label in accountSelectLabels)
            {
                switch (label.name)
                {
                    case "S3BucketNameLabel":
                        label.text = _bootstrapSettings.BucketName ?? "No Bucket Created";
                        break;
                    case "Region":
                        if (_awsUserProfilesPage.UpdateModel.RegionBootstrap.RegionIndex >= 0)
                        {
                            label.text =
                                _awsUserProfilesPage.UpdateModel.RegionBootstrap.AllRegions[
                                    _awsUserProfilesPage.UpdateModel.RegionBootstrap.RegionIndex];
                        }
                        break;
                    case "BootstrapStatus":
                        label.text = _bootstrapSettings.BucketName != null ? "Active" : "Inactive";
                        break;
                }
            }
        }

        private void UpdateModel(int index)
        {
            _awsUserProfilesPage.UpdateModel.SelectedProfileIndex = index;
            _awsUserProfilesPage.UpdateModel.Update();
            _stateManager.SelectedProfileName = _awsUserProfilesPage.UpdateModel.AllProlfileNames[index];
            _stateManager.CoreApi.PutSetting(SettingsKeys.CurrentProfileName,
                _stateManager.SelectedProfileName);
        }
    }
}