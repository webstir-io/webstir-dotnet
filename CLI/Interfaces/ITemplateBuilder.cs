namespace CLI.Interfaces;

public interface ITemplateBuilder
{
    string TemplateName { get; }
    string Description { get; }
    void CreateTemplate(string directory);
}