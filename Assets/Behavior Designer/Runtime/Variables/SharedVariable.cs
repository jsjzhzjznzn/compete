using UnityEngine;
using System;
using System.Reflection;

namespace BehaviorDesigner.Runtime
{
    public abstract class SharedVariable
    {
        [SerializeField]
        private bool mIsShared = false;
        public bool IsShared { get { return mIsShared; } set { mIsShared = value; } }

        [SerializeField]
        private bool mIsGlobal = false;
        public bool IsGlobal { get { return mIsGlobal; } set { mIsGlobal = value; } }

        [SerializeField]
        private bool mIsDynamic = false;
        public bool IsDynamic { get { return mIsDynamic; } set { mIsDynamic = value; } }

        [SerializeField]
        private string mName;
        public string Name { get { return mName; } set { mName = value; } }

#if UNITY_EDITOR
        [SerializeField]
        private string mToolTip;
        public string Tooltip { get { return mToolTip; } set { mToolTip = value; } }
#endif

        [SerializeField]
        private string mPropertyMapping;
        public string PropertyMapping { get { return mPropertyMapping; } set { mPropertyMapping = value; } }

        [SerializeField]
        private GameObject mPropertyMappingOwner;
        public GameObject PropertyMappingOwner { get { return mPropertyMappingOwner; } set { mPropertyMappingOwner = value; } }

        public bool IsNone { get { return mIsShared && string.IsNullOrEmpty(mName); } }

        public virtual void InitializePropertyMapping(BehaviorSource behaviorSource) { }

        public abstract object GetValue();
        public abstract void SetValue(object value);
    }

    public abstract class SharedVariable<T> : SharedVariable
    {
        private Func<T> mGetter;
        private Action<T> mSetter;

        public override void InitializePropertyMapping(BehaviorSource behaviorSource)
        {
            if (!BehaviorManager.IsPlaying) {
                return;
            }

            if (!string.IsNullOrEmpty(PropertyMapping)) {
                var propertyValue = PropertyMapping.Split('/');

                GameObject gameObject = null;
                try {
                    if (!Equals(PropertyMappingOwner, null)) {
                        gameObject = PropertyMappingOwner;
                    } else {
                        gameObject = (behaviorSource.Owner.GetObject() as Behavior).gameObject;
                    }
                } catch (Exception /*e*/) {
                    var behavior = behaviorSource.Owner.GetObject() as Behavior;
                    if (behavior != null && behavior.AsynchronousLoad) {
                        Debug.LogError("Error: Unable to retrieve GameObject. Properties cannot be mapped while using asynchronous load.");
                        return;
                    }
                }
                if (gameObject == null) {
                    Debug.LogError("Error: Unable to find GameObject on " + behaviorSource.behaviorName + " for property mapping with variable " + Name);
                    return;
                }
                var component = gameObject.GetComponent(TaskUtility.GetTypeWithinAssembly(propertyValue[0]));
                if (component == null) {
                    Debug.LogError("Error: Unable to find component on " + behaviorSource.behaviorName + " for property mapping with variable " + Name);
                    return;
                }
                var componentType = component.GetType();
                var property = componentType.GetProperty(propertyValue[1]);
                if (property != null) {
                    var propertyMethod = property.GetGetMethod();
                    if (propertyMethod != null) {
#if NETFX_CORE && !UNITY_EDITOR
                        mGetter = (Func<T>)propertyMethod.CreateDelegate(typeof(Func<T>), component);
#else
                        mGetter = (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), component, propertyMethod);
#endif
                    }
                    propertyMethod = property.GetSetMethod();
                    if (propertyMethod != null) {
#if NETFX_CORE && !UNITY_EDITOR
                        mSetter = (Action<T>)propertyMethod.CreateDelegate(typeof(Action<T>), component);
#else
                        mSetter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), component, propertyMethod);
#endif
                    }
                }
            }
        }

        public T Value
        {
            get { return (mGetter != null ? mGetter() : mValue); }
            set
            {
                if (mSetter != null) {
                    mSetter(value);
                } else {
                    mValue = value;
                }
            }
        }
        [SerializeField]
        protected T mValue;

        public override object GetValue() { return Value; }
        public override void SetValue(object value)
        {
            if (mSetter != null) {
                mSetter((T)value);
            } else {
                if (value is IConvertible) {
                    mValue = (T)Convert.ChangeType(value, typeof(T));
                } else {
                    mValue = (T)value;
                }
            }
        }

        public override string ToString()
        {
            return (Value == null ? "(null)" : Value.ToString());
        }
    }
}