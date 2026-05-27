using System.Runtime.Serialization;

namespace AgentSmith.Contracts.Models.Configuration;

public enum TrackerType
{
    [EnumMember(Value = "github")] GitHub,
    [EnumMember(Value = "gitlab")] GitLab,
    [EnumMember(Value = "azure_devops")] AzureDevOps,
    [EnumMember(Value = "jira")] Jira
}
