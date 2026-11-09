using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestBuilder.Runtime
{
    [CreateAssetMenu(menuName = "RestBuilder/Endpoints Config", fileName = "EndPointsConfig")]
    public sealed class EndPointsConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Endpoint
        {
            public string Key;
            public string BaseUrl;
            [Tooltip("Optional path, e.g. /users/{id}")]
            public string EndPoint;
        }

        [SerializeField] private List<Endpoint> _endpoints = new();

        public IReadOnlyList<Endpoint> Endpoints => _endpoints;

        public Endpoint Get(string key)
        {
            foreach (var e in _endpoints)
                if (e.Key == key) return e;
            throw new KeyNotFoundException($"Endpoint '{key}' not found.");
        }

        public RestClient CreateClient(string key, bool logEnabled = true)
        {
            var e = Get(key);
            return new RestClient(e.BaseUrl, logEnabled);
        }
    }
}