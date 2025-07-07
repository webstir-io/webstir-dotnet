namespace CLI.Interfaces;

public interface IPageWorker : IFileWorker
{
    void AddPage(DirectoryInfo pageDirectory);
}