// Models/DocumentModel.cs
namespace AzureOpenAIDemo.Api.Models
{
    public class DocumentModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}