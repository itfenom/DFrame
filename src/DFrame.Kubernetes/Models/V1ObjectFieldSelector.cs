﻿namespace DFrame.KubernetesWorker.Models
{
    public class V1ObjectFieldSelector
    {
        public string apiVersion { get; set; }
        public string fieldPath { get; set; }
    }
}