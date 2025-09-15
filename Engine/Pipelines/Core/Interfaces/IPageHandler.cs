using System.Threading.Tasks;

namespace Engine.Pipelines.Core.Interfaces;

public interface IPageHandler : IFrontendHandler
{
    Task<bool> AddPageAsync(string pageName);
}
