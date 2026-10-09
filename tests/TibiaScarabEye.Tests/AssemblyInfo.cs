using Xunit;

// Os testes de desktop criam janelas reais e mexem em estado global do processo (DPI, estilos visuais).
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
