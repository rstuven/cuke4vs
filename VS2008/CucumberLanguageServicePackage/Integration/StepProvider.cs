﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Samples.LinqToCodeModel.Extensions;
using EnvDTE;

namespace CucumberLanguageServices.Integration
{
    public class StepProvider
    {
        public static readonly string[] STEP_ATTRIBUTES = {
                                                               "Cuke4Nuke.Framework.GivenAttribute",
                                                               "Cuke4Nuke.Framework.WhenAttribute",
                                                               "Cuke4Nuke.Framework.ThenAttribute",
                                                               "TechTalk.SpecFlow.GivenAttribute",
                                                               "TechTalk.SpecFlow.WhenAttribute",
                                                               "TechTalk.SpecFlow.ThenAttribute",
                                                           };

        public static readonly string PENDING_ATTRIBUTE = "Cuke4Nuke.Framework.PendingAttribute";

        protected readonly List<StepDefinition> _stepDefinitions = new List<StepDefinition>();

        public void ProcessItem(ProjectItem item)
        {
            if (item == null || item.FileCodeModel == null)
                return;

            foreach (var codeClass in item.FileCodeModel.GetIEnumerable<CodeClass>())
            {
                ProcessItem(codeClass);
            }
        }

        public void ProcessItem(CodeClass codeClass)
        {
            if (codeClass == null) return;
            lock (this)
            {
                RemoveAttributesFor(codeClass.FullName);

                foreach (var attribute in codeClass.GetIEnumerable<CodeAttribute>())
                {
                    var parent = attribute.Parent as CodeElement;

                    Debug.Print("StepProvider: Add '{0}', parent='{1}'", attribute.Name, parent != null ? parent.Name : "<null>");

                    AddStep(attribute, codeClass);
                }
            }
        }

        private void AddStep(CodeAttribute attribute, CodeClass codeClass)
        {
            if (!STEP_ATTRIBUTES.Contains(attribute.FullName))
                return;

            var attributeValue = GetAttributeValue(attribute, codeClass);

            _stepDefinitions.Add(new StepDefinition(Unescape(attributeValue))
                                     {
                                         ProjectItem = attribute.ProjectItem,
                                         StartPoint = attribute.StartPoint,
                                         EndPoint = attribute.EndPoint,
                                         Function = attribute.Parent as CodeFunction,
                                         ClassName = codeClass.FullName
                                     });

            Debug.Print("Attribute FullName={0}, Value={1}, Unescaped={4} at {2}:{3}",
                        attribute.FullName, attribute.Value, attribute.StartPoint.Line, attribute.StartPoint.DisplayColumn,
                        Unescape(attribute.Value));
        }

        private static string GetAttributeValue(CodeAttribute attribute, CodeClass codeClass)
        {
            var value = attribute.Value;
            if (!string.IsNullOrEmpty(value))
            {
                if (value[0] != '"' && value[0] != '@')
                {
                    var variable = codeClass.GetIEnumerable<CodeVariable>()
                        .Where(v => v.IsConstant && v.Name == value)
                        .FirstOrDefault();

                    if (variable != null)
                    {
                        var initExpression = variable.InitExpression as string;
                        if (initExpression != null)
                        {
                            value = initExpression;
                        }
                    }
                }
            }
            return value;
        }

        public StepDefinition[] FindMatchesFor(string stepIdentifier)
        {
            lock (this)
            {
                return _stepDefinitions.Where(step => step.Matches(stepIdentifier)).ToArray();
            }
        }

        public bool HasMatchFor(string stepIdentifier)
        {
            return FindMatchesFor(stepIdentifier).Length > 0;
        }

        public IEnumerable<StepDefinition> StepDefinitions
        {
            get
            {
                lock (this)
                {
                    return _stepDefinitions.OrderBy(step => step.Value).ToList();
                }
            }
        }

        private void RemoveAttributesFor(string className)
        {
            var result = _stepDefinitions.RemoveAll(step => step.ClassName == className);
            Debug.Print("StepProvider: Removed {0} steps for {1}", result, className);
        }

        public static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
                return value;

            var unescape = true;
            var result = value;

            if (result[0] == '@')
            {
                result = result.Substring(1);
                unescape = false;
            }

            if (result[0] != '"' || result[result.Length - 1] != '"')
                return result;

            result = result.Substring(1, result.Length - 2);
            
            return unescape ? Regex.Unescape(result) : result;
        }

        public void Clear()
        {
            lock(this)
            {
                _stepDefinitions.Clear();
            }
        }
    }
}
