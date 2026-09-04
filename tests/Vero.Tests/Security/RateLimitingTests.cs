namespace Vero.Tests.Security;

/// <summary>
/// Testes do mecanismo de sliding window usado no rate limiting.
/// Testa a classe SlidingWindow diretamente (é internal, mas no mesmo assembly via InternalsVisibleTo
/// ou testamos o comportamento indiretamente).
/// Aqui testamos a lógica conceitual do rate limiting.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public void SlidingWindow_DentroDoLimite_DevePermitir()
    {
        var window = new TestSlidingWindow();
        var limite = 5;
        var janela = TimeSpan.FromSeconds(60);

        // 5 requisições dentro do limite
        for (int i = 0; i < limite; i++)
        {
            Assert.True(window.TentarRegistrar(limite, janela));
        }
    }

    [Fact]
    public void SlidingWindow_AcimaDoLimite_DeveBloquear()
    {
        var window = new TestSlidingWindow();
        var limite = 3;
        var janela = TimeSpan.FromSeconds(60);

        // Preenche o limite
        for (int i = 0; i < limite; i++)
        {
            Assert.True(window.TentarRegistrar(limite, janela));
        }

        // A próxima deve ser bloqueada
        Assert.False(window.TentarRegistrar(limite, janela));
    }

    [Fact]
    public void SlidingWindow_AposExpiracaoDaJanela_DevePermitirNovamente()
    {
        var window = new TestSlidingWindow();
        var limite = 2;
        var janela = TimeSpan.FromMilliseconds(100);

        // Preenche o limite
        Assert.True(window.TentarRegistrar(limite, janela));
        Assert.True(window.TentarRegistrar(limite, janela));
        Assert.False(window.TentarRegistrar(limite, janela));

        // Espera a janela expirar
        Thread.Sleep(150);

        // Deve permitir novamente
        Assert.True(window.TentarRegistrar(limite, janela));
    }

    /// <summary>
    /// Réplica da SlidingWindow do middleware para testes unitários isolados.
    /// </summary>
    private class TestSlidingWindow
    {
        private readonly Queue<DateTime> _timestamps = new();

        public bool TentarRegistrar(int limite, TimeSpan janela)
        {
            var agora = DateTime.UtcNow;
            var limiteInferior = agora.Subtract(janela);

            while (_timestamps.Count > 0 && _timestamps.Peek() < limiteInferior)
            {
                _timestamps.Dequeue();
            }

            if (_timestamps.Count >= limite)
            {
                return false;
            }

            _timestamps.Enqueue(agora);
            return true;
        }
    }
}
