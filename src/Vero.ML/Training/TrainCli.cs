namespace Vero.ML.Training;

/// <summary>
/// CLI simples para treinar o modelo de detecção de fraude.
/// Uso: dotnet run --project src/Vero.ML -- train [caminho_modelo]
/// </summary>
public static class TrainCli
{
    public static void Run(string[] args)
    {
        var modelPath = args.Length > 1
            ? args[1]
            : Path.Combine(AppContext.BaseDirectory, "Models", "fraud_model.zip");

        Console.WriteLine("╔═══════════════════════════════════════╗");
        Console.WriteLine("║  Vero ML — Treinamento de Modelo      ║");
        Console.WriteLine("╚═══════════════════════════════════════╝");
        Console.WriteLine();

        Console.WriteLine("Gerando 10.000 amostras sintéticas...");
        var trainer = new ModelTrainer();
        var metrics = trainer.TreinarESalvar(modelPath);

        Console.WriteLine();
        Console.WriteLine(metrics.ToString());
    }
}
