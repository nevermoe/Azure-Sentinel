﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Azure.Sentinel.Analytics.Management.AnalyticsTemplatesService.Interface.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using YamlDotNet.Serialization;

namespace Kqlvalidations.Tests
{
    public class DetectionTemplateSchemaValidationTests
    {
        private static readonly string DetectionPath = DetectionsYamlFilesTestData.GetDetectionPath();

        [Theory]
        [ClassData(typeof(DetectionsYamlFilesTestData))]
        public void Validate_DetectionTemplates_HaveValidTemplateStructure(string detectionsYamlFileName)
        {
            Action<ScheduledTemplateInternalModel> validateConnectorSchema = (ScheduledTemplateInternalModel templateObject) => {
                var validationContext = new ValidationContext(templateObject);
                Validator.ValidateObject(templateObject, validationContext, true);
            };

            ValidateDetectionTemplate(detectionsYamlFileName, validateConnectorSchema);
        }

        [Theory]
        [ClassData(typeof(DetectionsYamlFilesTestData))]
        public void Validate_DetectionTemplates_HaveValidConnectorsIds(string detectionsYamlFileName)
        {
            Action<ScheduledTemplateInternalModel> validateConnectorsIds = (ScheduledTemplateInternalModel templateObject) => {
                List<string> connectorIds = templateObject.RequiredDataConnectors
                    .Where(requiredDataConnector => requiredDataConnector.ConnectorId != null)
                    .Select(requiredDataConnector => requiredDataConnector.ConnectorId).ToList();

                connectorIds.ForEach(connectorId => {
                    if (!TemplatesSchemaValidationsReader.ValidConnectorIds.Contains(connectorId))
                    {
                        throw new FormatException($"Not valid connectorId: {connectorId}. If a new connector is used and already configured in the Portal, please add it's Id to the list in 'ValidConnectorIds.json' file.");
                    }
                });
            };

            ValidateDetectionTemplate(detectionsYamlFileName, validateConnectorsIds);
        }


        private void ValidateDetectionTemplate(string detectionsYamlFileName, Action<ScheduledTemplateInternalModel> testAction)
        {
            var detectionsYamlFile = Directory.GetFiles(DetectionPath, detectionsYamlFileName, SearchOption.AllDirectories).Single();
            var yaml = File.ReadAllText(detectionsYamlFile);

            //we ignore known issues (in progress)
            foreach (var templateToSkip in TemplatesSchemaValidationsReader.WhiteListTemplateIds)
            {
                if (yaml.Contains(templateToSkip) || detectionsYamlFile.Contains(templateToSkip))
                {
                    return;
                }
            }

            var jObj = JObject.Parse(ConvertYamlToJson(yaml));

            var exception = Record.Exception(() =>
            {
                var templateObject = jObj.ToObject<ScheduledTemplateInternalModel>();
                testAction(templateObject);
            });

            exception.Should().BeNull();
        }

        public static string ConvertYamlToJson(string yaml)
        {
            var deserializer = new Deserializer();
            var yamlObject = deserializer.Deserialize<object>(yaml);

            using (var jsonObject = new StringWriter())
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonObject, yamlObject);
                return jsonObject.ToString();
            }
        }
    }
}
