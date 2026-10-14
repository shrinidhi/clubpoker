using System;
using UnityEngine;

namespace ClubPoker.Core
{
    [CreateAssetMenu(fileName = "AppConfig", menuName = "ClubPoker/AppConfig")]
    public class AppConfig : ScriptableObject
    {
        [Header("Environment")]
        public string environmentName;

        [Header("API Settings")]
        public string apiBaseUrl;
        public string webSocketUrl;

        [Header("Social Login")]
        [Tooltip("Google Cloud WEB OAuth client ID — the audience the server verifies " +
                 "Google ID tokens against. Not secret. Never put the client secret here.")]
        public string googleWebClientId;

        [Header("App Version")]
        public string minimumAppVersion;

        [Header("Logging")]
        public LogLevel logLevel;

        [Header("Feature Flags")]
        public FeatureFlag[] featureFlags;
    }

    [Serializable]
    public class FeatureFlag
    {
        public string flagName;
        public bool defaultValue;
    }

    public enum LogLevel
    {
        None,
        Error,
        Warning,
        Info,
        Debug
    }
}