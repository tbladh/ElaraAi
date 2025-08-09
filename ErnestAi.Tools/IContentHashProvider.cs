namespace ErnestAi.Tools
{
    public interface IContentHashProvider
    {
        string ComputeMd5(string input);
    }
}
