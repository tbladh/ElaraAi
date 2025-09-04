namespace Elara.Context
{
    [System.Text.Json.Serialization.JsonConverter(typeof(ChatRoleJsonConverter))]
    public enum ChatRole
    {
        User,
        Assistant,
        System
    }
}
