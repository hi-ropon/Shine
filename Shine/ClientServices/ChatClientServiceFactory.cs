namespace Shine
{
    public static class ChatClientServiceFactory
    {
        public static IChatClientService CreateFromOptions()
        {
            var pkg = ShinePackage.Instance;
            if (pkg == null) return null;

            var opts = (AiAssistantOptions)pkg.GetDialogPage(typeof(AiAssistantOptions));

            return opts.Provider switch
            {
                OpenAiProvider.OpenAI => new OpenAiClientService(
                    opts.OpenAIApiKey,
                    "o4-mini",
                    opts.Temperature,
                    "high"),

                OpenAiProvider.AzureOpenAI => new AzureOpenAiClientService(
                    opts.AzureOpenAIEndpoint,
                    opts.AzureOpenAIApiKey,
                    opts.AzureDeploymentName,
                    opts.Temperature),

                _ => null
            };
        }
    }
}
