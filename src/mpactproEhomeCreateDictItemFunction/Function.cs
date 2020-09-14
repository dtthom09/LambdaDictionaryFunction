using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using BrickBridge.Models;
using PodioCore.Models;
using PodioCore.Items;
using PodioCore.Utils.ItemFields;
using BrickBridge;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace mpactproEhomeCreateDictItemFunction
{
    public class Function:saasafrasLambdaBaseFunction.Function
    {
        private int GetFieldIdEhome(string key)
        {
            var field = _deployedSpacesEhome[key];
            return int.Parse(field);
        }
        private Dictionary<string, string> _deployedSpacesEhome;

        //creates ehome dictionary item
        public override async System.Threading.Tasks.Task InnerHandler(RoutedPodioEvent e, ILambdaContext lambda_ctx)
        {
            lambda_ctx.Logger.LogLine($"Lambda_ctx: {lambda_ctx.Identity}");
            System.Environment.SetEnvironmentVariable("PODIO_PROXY_URL", Config.PODIO_PROXY_URL);
            System.Environment.SetEnvironmentVariable("BBC_SERVICE_URL", Config.BBC_SERVICE_URL);
            System.Environment.SetEnvironmentVariable("BBC_SERVICE_API_KEY", Config.BBC_SERVICE_API_KEY);

            string url = Config.LOCKER_URL;
            string key = Config.BBC_SERVICE_API_KEY;
            var functionName = "mpactproEhomeCreateDictItemFunction";
            var uniqueId = e.podioEvent.type; //this is using the ehome id for now since we dont have a unique item id
            var client = new BbcServiceClient(url, key);
            var lockValue = await client.LockFunction(functionName, uniqueId);
            if (string.IsNullOrEmpty(lockValue))
            {
                lambda_ctx.Logger.LogLine($"Failed to acquire lock for {functionName} and id {uniqueId}");
                return;
            }
            try
            {
                var factory = new AuditedPodioClientFactory(e.appId, e.version, e.clientId, e.currentEnvironment.environmentId);
                var podioClient = factory.Client();

                //eHome landing dictionary
                EhomeDictionary dict = new EhomeDictionary();
                _deployedSpacesEhome = dict.Dictionary;
                var ehomeId = e.podioEvent.type;  //TODO: update if key gets assigned to a new property
                var appId = 20806986; //will always be the same
                Item ehomeItem = new Item();

                void setField(string fieldName, string fieldValue)
                {
                    var fieldId = GetFieldIdEhome($"*eHome America Landing Space|eHome America Profile Dictionary|{fieldName}");
                    var textItemField = ehomeItem.Field<TextItemField>(fieldId);
                    textItemField.Value = fieldValue;
                }

                setField("Client ID", e.clientId);
                setField("Environment ID", e.currentEnvironment.environmentId);
                setField("eHome Profile ID", ehomeId);

                await podioClient.CreateItem(ehomeItem, appId, true);
                lambda_ctx.Logger.LogLine("eHome dict item has ben created!");
            }
            catch (Exception ex)
            {
                lambda_ctx.Logger.LogLine($"The function {functionName} failed");
                lambda_ctx.Logger.LogLine($"Exception: {ex}");
            }
            finally
            {
                await client.UnlockFunction(functionName, uniqueId, lockValue);
            }
        }
    }
}