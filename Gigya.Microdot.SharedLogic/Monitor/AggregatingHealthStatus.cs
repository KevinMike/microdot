﻿#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Concurrent;
using System.Linq;
using Metrics;

namespace Gigya.Microdot.SharedLogic.Monitor
{
    public class AggregatingHealthStatus
    {
        private readonly ConcurrentDictionary<string, Func<HealthCheckResult>> _checks = new ConcurrentDictionary<string, Func<HealthCheckResult>>();

        public AggregatingHealthStatus(string componentName, IHealthMonitor healthMonitor)
        {
            healthMonitor.SetHealthFunction(componentName, HealthCheck);
        }

        private HealthCheckResult HealthCheck()
        {
            var results =_checks
                .Select(c => new {c.Key, Result = c.Value()})
                .OrderBy(c => c.Result.IsHealthy)
                .ThenBy(c => c.Key)
                .ToArray();

            bool healthy = results.All(r => r.Result.IsHealthy);
            string message = string.Join("\r\n", results.Select(r => (r.Result.IsHealthy ? "[OK] " : "[Unhealthy] ") + r.Result.Message));

            return healthy ? HealthCheckResult.Healthy(message) : HealthCheckResult.Unhealthy(message);
        }


        public void RegisterCheck(string name, Func<HealthCheckResult> checkFunc)
        {
            _checks.AddOrUpdate(name, checkFunc, (a, b) => checkFunc);
        }

        public void RemoveCheck(string name)
        {
            _checks.TryRemove(name, out var _);
        }
    }
}