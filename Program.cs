namespace OpenRender.Launcher;

public static class Program
{
    /// <summary>
    /// Reenvía la ejecución al proyecto UI para que `dotnet run`
    /// funcione directamente desde la raíz del repositorio.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        OpenRender.Program.Main(args);
    }
}
