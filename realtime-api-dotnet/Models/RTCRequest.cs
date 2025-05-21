// Models/RTCRequest.cs
using AzureOpenAIDemo.Api.Models;

public class RTCRequest
{
    public string Sdp { get; set; }
    public string EphemeralKey { get; set; }
    public string DeploymentName { get; set; }
    public string Region { get; set; }
    public RAGOptions? RagOptions { get; set; }
}