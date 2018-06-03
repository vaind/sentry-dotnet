using System;
using System.Threading.Tasks;
using Sentry.Protocol;

namespace Sentry.Extensibility
{
    /// <summary>
    /// Scope management
    /// </summary>
    /// <remarks>
    /// An implementation shall be create new scopes and allow consumers
    /// modify the current scope
    /// </remarks>
    public interface ISentryScopeManagement
    {
        /// <summary>
        /// Configures the current scope.
        /// </summary>
        /// <param name="configureScope">The configure scope.</param>
        void ConfigureScope(Action<Scope> configureScope);

        /// <summary>
        /// Asynchronouly configure the current scope.
        /// </summary>
        /// <param name="configureScope">The configure scope.</param>
        /// <returns>A task that completes when the callback is done or a completed task if the SDK is disabled.</returns>
        Task ConfigureScopeAsync(Func<Scope, Task> configureScope);

        /// <summary>
        /// Pushes a new scope into the stack which is removed upon Dispose
        /// </summary>
        /// <returns>A disposable which removes the scope
        /// from the environment when invoked</returns>
        IDisposable PushScope();

        /// <summary>
        /// Pushes a new scope into the stack which is removed upon Dispose
        /// </summary>
        /// <param name="state">A state to associate with the scope</param>
        /// <typeparam name="TState"></typeparam>
        /// <returns>A disposable which removes the scope
        /// from the environment when invoked</returns>
        IDisposable PushScope<TState>(TState state);
    }
}
