using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransitStats.Systems
{
    // Helper base class for custom Selected Info Panel sections
    // Based on Recolor mod's pattern by yenyang

    using System;
    using Colossal.UI.Binding;
    using Game.UI.InGame;

    namespace TransitTransfers.Extensions
    {
        /// <summary>
        /// Base class for creating custom sections in the Selected Info Panel.
        /// Provides helper methods for creating value bindings and triggers.
        /// </summary>
        public abstract partial class ExtendedInfoSectionBase : InfoSectionBase
        {
            /// <summary>
            /// Creates a value binding with a getter and setter.
            /// </summary>
            public ValueBindingHelper<T> CreateBinding<T>(string key, T initialValue)
            {
                var helper = new ValueBindingHelper<T>(
                    new ValueBinding<T>(ModId, key, initialValue, new GenericUIWriter<T>()));

                AddBinding(helper.Binding);
                return helper;
            }

            /// <summary>
            /// Creates a value binding with a setter trigger callback.
            /// </summary>
            public ValueBindingHelper<T> CreateBinding<T>(string key, string setterKey, T initialValue, Action<T> updateCallBack = null)
            {
                var helper = new ValueBindingHelper<T>(
                    new ValueBinding<T>(ModId, key, initialValue, new GenericUIWriter<T>()),
                    updateCallBack);
                var trigger = new TriggerBinding<T>(ModId, setterKey, helper.UpdateCallback, new GenericUIReader<T>());

                AddBinding(helper.Binding);
                AddBinding(trigger);
                return helper;
            }

            /// <summary>
            /// Creates a getter-only value binding.
            /// </summary>
            public GetterValueBinding<T> CreateBinding<T>(string key, Func<T> getterFunc)
            {
                var binding = new GetterValueBinding<T>(ModId, key, getterFunc, new GenericUIWriter<T>());
                AddBinding(binding);
                return binding;
            }

            /// <summary>
            /// Creates a trigger binding with no parameters.
            /// </summary>
            public TriggerBinding CreateTrigger(string key, Action action)
            {
                var binding = new TriggerBinding(ModId, key, action);
                AddBinding(binding);
                return binding;
            }

            /// <summary>
            /// Creates a trigger binding with one parameter.
            /// </summary>
            public TriggerBinding<T1> CreateTrigger<T1>(string key, Action<T1> action)
            {
                var binding = new TriggerBinding<T1>(ModId, key, action, new GenericUIReader<T1>());
                AddBinding(binding);
                return binding;
            }

            /// <summary>
            /// Override this to return your mod's unique identifier.
            /// </summary>
            protected abstract string ModId { get; }
        }

        /// <summary>
        /// Helper class for managing value bindings with update callbacks.
        /// </summary>
        public class ValueBindingHelper<T>
        {
            public ValueBinding<T> Binding { get; }
            private readonly Action<T> updateCallback;

            public ValueBindingHelper(ValueBinding<T> binding, Action<T> updateCallback = null)
            {
                Binding = binding;
                this.updateCallback = updateCallback;
            }

            public T Value
            {
                get => Binding.value;
                set
                {
                    Binding.Update(value);
                    updateCallback?.Invoke(value);
                }
            }

            public void Update(T value)
            {
                Value = value;
            }

            public void UpdateCallback(T value)
            {
                Binding.Update(value);
                updateCallback?.Invoke(value);
            }
        }
    }

}
