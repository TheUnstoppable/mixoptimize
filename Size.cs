namespace mixoptimize;

public record Size(uint Width, uint Height)
{
    public override string ToString()
    {
        return $"{Width}x{Height}";
    }
}