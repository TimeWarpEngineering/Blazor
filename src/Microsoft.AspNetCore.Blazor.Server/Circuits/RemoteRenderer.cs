// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Blazor.Components;
using Microsoft.AspNetCore.Blazor.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Blazor.Browser.Rendering
{
    internal class RemoteRenderer : Renderer
    {
        // The purpose of the timeout is just to ensure server resources are released at some
        // point if the client disconnects without sending back an ACK after a render
        private const int TimeoutMilliseconds = 60 * 1000;

        private readonly int _id;
        private readonly IClientProxy _client;
        private readonly IJSRuntime _jsRuntime;
        private readonly RendererRegistry _rendererRegistry;
        private readonly ConcurrentDictionary<long, AutoCancelTaskCompletionSource<object>> _pendingRenders
            = new ConcurrentDictionary<long, AutoCancelTaskCompletionSource<object>>();
        private long _nextRenderId = 1;

        /// <summary>
        /// Notifies when a rendering exception occured.
        /// </summary>
        public event EventHandler<Exception> UnhandledException;

        /// <summary>
        /// Creates a new <see cref="RemoteRenderer"/>.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <param name="rendererRegistry">The <see cref="RendererRegistry"/>.</param>
        /// <param name="jsRuntime">The <see cref="IJSRuntime"/>.</param>
        /// <param name="client">The <see cref="IClientProxy"/>.</param>
        public RemoteRenderer(
            IServiceProvider serviceProvider,
            RendererRegistry rendererRegistry,
            IJSRuntime jsRuntime,
            IClientProxy client)
            : base(serviceProvider)
        {
            _rendererRegistry = rendererRegistry;
            _jsRuntime = jsRuntime;
            _client = client;

            _id = _rendererRegistry.Add(this);
        }

        /// <summary>
        /// Attaches a new root component to the renderer,
        /// causing it to be displayed in the specified DOM element.
        /// </summary>
        /// <typeparam name="TComponent">The type of the component.</typeparam>
        /// <param name="domElementSelector">A CSS selector that uniquely identifies a DOM element.</param>
        public void AddComponent<TComponent>(string domElementSelector)
            where TComponent: IComponent
        {
            AddComponent(typeof(TComponent), domElementSelector);
        }

        /// <summary>
        /// Associates the <see cref="IComponent"/> with the <see cref="BrowserRenderer"/>,
        /// causing it to be displayed in the specified DOM element.
        /// </summary>
        /// <param name="componentType">The type of the component.</param>
        /// <param name="domElementSelector">A CSS selector that uniquely identifies a DOM element.</param>
        public void AddComponent(Type componentType, string domElementSelector)
        {
            var component = InstantiateComponent(componentType);
            var componentId = AssignRootComponentId(component);

            var attachComponentTask = _jsRuntime.InvokeAsync<object>(
                "Blazor._internal.attachRootComponentToElement",
                _id,
                domElementSelector,
                componentId);
            CaptureAsyncExceptions(attachComponentTask);

            RenderRootComponent(componentId);
        }

        /// <summary>
        /// Disposes the instance.
        /// </summary>
        public void Dispose()
        {
            _rendererRegistry.TryRemove(_id);
        }

        /// <inheritdoc />
        protected override Task UpdateDisplay(in RenderBatch batch)
        {
            // Prepare to track the render process with a timeout
            var renderId = Interlocked.Increment(ref _nextRenderId);
            var pendingRenderInfo = new AutoCancelTaskCompletionSource<object>(TimeoutMilliseconds);
            _pendingRenders[renderId] = pendingRenderInfo;

            // Send the render batch to the client
            // If the "send" operation fails, abort the whole render with that exception
            _client.SendAsync("JS.RenderBatch", _id, renderId, batch).ContinueWith(sendTask =>
            {
                if (sendTask.IsFaulted)
                {
                    pendingRenderInfo.TrySetException(sendTask.Exception);
                }
            });

            // When the render is completed (success, fail, or timeout), stop tracking it
            return pendingRenderInfo.Task.ContinueWith(task =>
            {
                _pendingRenders.TryRemove(renderId, out var ignored);
                if (task.IsFaulted)
                {
                    UnhandledException?.Invoke(this, task.Exception);
                }
            });
        }

        public void OnRenderCompleted(long renderId, string errorMessageOrNull)
        {
            if (_pendingRenders.TryGetValue(renderId, out var pendingRenderInfo))
            {
                if (errorMessageOrNull == null)
                {
                    pendingRenderInfo.TrySetResult(null);
                }
                else
                {
                    pendingRenderInfo.TrySetException(
                        new RemoteRendererException(errorMessageOrNull));
                }
            }
        }

        private void CaptureAsyncExceptions(Task task)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    UnhandledException?.Invoke(this, t.Exception);
                }
            });
        }
    }
}
