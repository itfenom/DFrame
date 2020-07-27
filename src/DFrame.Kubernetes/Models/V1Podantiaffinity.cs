﻿using System.Collections.Generic;

namespace DFrame.KubernetesWorker.Models
{
    public class V1Podantiaffinity
    {
        public IList<V1WeightedPodAffinityTerm> preferredDuringSchedulingIgnoredDuringExecution { get; set; }
        public IList<V1PodAffinityTerm> requiredDuringSchedulingIgnoredDuringExecution { get; set; }
    }
}