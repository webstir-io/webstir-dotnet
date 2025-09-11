namespace Engine.Pipelines.JavaScript.Minification;

internal enum MinifyMode
{
    Code,
    SingleQuote,
    DoubleQuote,
    Template,
    TemplateExpr,
    Regex
}

internal sealed class MinifyState
{
    public MinifyMode Mode { get; set; } = MinifyMode.Code;
    public int Index
    {
        get; set;
    }
    public int TemplateExprDepth
    {
        get; set;
    }
    public bool InCharacterClass
    {
        get; set;
    }
    public char LastNonWhitespaceChar { get; set; } = '\0';
}
