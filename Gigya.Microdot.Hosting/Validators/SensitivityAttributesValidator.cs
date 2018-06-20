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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting.Validators
{
    public class SensitivityAttributesValidator : IValidator
    {
        private readonly IServiceInterfaceMapper _serviceInterfaceMapper;

        public SensitivityAttributesValidator(IServiceInterfaceMapper serviceInterfaceMapper)
        {
            _serviceInterfaceMapper = serviceInterfaceMapper;
        }

        public void Validate()
        {
            foreach (var serviceInterface in _serviceInterfaceMapper.ServiceInterfaceTypes)
            {
                foreach (var method in serviceInterface.GetMethods())
                {
                    if (method.GetCustomAttribute(typeof(SensitiveAttribute)) != null && method.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
                        throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same method ({method.Name}) on serviceInterface ({serviceInterface.Name})");

                    foreach (var parameter in method.GetParameters())
                    {
                        if (parameter.GetCustomAttribute(typeof(SensitiveAttribute)) != null && parameter.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
                        {
                            throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same parameter ({parameter.Name}) in method ({method.Name}) on serviceInterface ({serviceInterface.Name})");
                        }

                        var logFieldExists = Attribute.IsDefined(parameter, typeof(LogFieldsAttribute));

                        if (parameter.ParameterType.IsClass && parameter.ParameterType.FullName?.StartsWith("System.") == false)
                        {
                            var stack = new Stack<string>();
                            if (FindFieldWithAttribute(parameter.ParameterType, stack))
                                if (!logFieldExists)
                                    throw new ProgrammaticException($"The method '{method.Name}' parameter '{parameter.Name}' has a member '{string.Join(" --> ", stack)}' that is marked as [Sensitive] or [NonSensitive], but the method parameter is not marked with [LogFields]");
                                else if (stack.Count > 1)
                                    throw new ProgrammaticException($"The method '{method.Name}' parameter '{parameter.Name}' has a member '{string.Join(" --> ", stack)}' that is marked as [Sensitive] or [NonSensitive], but these are only allowed on the root object");
                        }
                    }
                }
            }
        }


        private bool FindFieldWithAttribute(Type type, Stack<string> path)
        {
            if (type.IsClass == false || type.FullName?.StartsWith("System.") == true)
                return false;

            path.Push(type.FullName);

            foreach (var memberInfo in type.FindMembers(MemberTypes.Property | MemberTypes.Field, BindingFlags.Public | BindingFlags.Instance, null, null)
                                           .Where(x => x is FieldInfo || (x is PropertyInfo propertyInfo) && propertyInfo.CanRead))
                if (   FindFieldWithAttribute(memberInfo is PropertyInfo propertyInfo ? propertyInfo.PropertyType : ((FieldInfo)memberInfo).FieldType, path)
                    || memberInfo.GetCustomAttribute(typeof(SensitiveAttribute)) != null
                    || memberInfo.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
                    return true;

            path.Pop();
            return false;
        }
    }
}