using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 自訂 Hangfire JobActivator，支援依賴注入
/// </summary>
public class ServiceProviderJobActivator : JobActivator
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceProviderJobActivator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override object ActivateJob(Type jobType)
    {
        return _serviceProvider.GetRequiredService(jobType);
    }

    public override JobActivatorScope BeginScope(JobActivatorContext context)
    {
        return new ServiceProviderJobActivatorScope(_serviceProvider.CreateScope());
    }

    private class ServiceProviderJobActivatorScope : JobActivatorScope
    {
        private readonly IServiceScope _scope;

        public ServiceProviderJobActivatorScope(IServiceScope scope)
        {
            _scope = scope;
        }

        public override object Resolve(Type type)
        {
            return _scope.ServiceProvider.GetRequiredService(type);
        }

        public override void DisposeScope()
        {
            _scope.Dispose();
        }
    }
}
