// Models/RAGOptions.cs
namespace AzureOpenAIDemo.Api.Models
{
    public class RAGOptions
    {
        public bool Enabled { get; set; } = false;
        public int TopK { get; set; } = 3;
        public float RelevanceThreshold { get; set; } = 0.7f;
        public string SearchQuery { get; set; } = string.Empty;
    }
}