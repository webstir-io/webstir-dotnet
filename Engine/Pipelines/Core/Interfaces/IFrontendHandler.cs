using System.Threading.Tasks;

namespace Engine.Pipelines.Core.Interfaces;

public interface IFrontendHandler
{
    int BuildOrder { get; }
    int PublishOrder { get; }

    Task BuildAsync(string? changedFilePath = null);
    Task PublishAsync();
}

