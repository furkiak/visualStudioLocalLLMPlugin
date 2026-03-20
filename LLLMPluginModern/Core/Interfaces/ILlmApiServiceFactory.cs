namespace LLLMPluginModern.Core.Interfaces
{
    public interface ILlmApiServiceFactory
    {
        ILlmApiService Create(string providerName);
    }
}