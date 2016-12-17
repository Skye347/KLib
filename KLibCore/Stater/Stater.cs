using System;
using System.Collections;
using System.Collections.Generic;

namespace KLib.State
{
    public class MachineState<TKey, TMethodRet, TMethodAgr>
    {
		public MachineState(){
        }
		public TMethodRet Call(TMethodAgr agr){
            return StateMethod[currentState](this,agr);
        }
        private TKey _currentState;
        public TKey currentState{
			set{
				if(StateMethod.ContainsKey(value)){
                    this._currentState = value;
                }
			}
			get{
                return _currentState;
            }
		}
        public void Add(TKey key,Func<MachineState<TKey,TMethodRet,TMethodAgr>,TMethodAgr, TMethodRet> method){
			if(!StateMethod.ContainsKey(key)){
                StateMethod.Add(key, method);
            }
		}
        private Dictionary<TKey, Func<MachineState<TKey,TMethodRet,TMethodAgr>,TMethodAgr, TMethodRet>> StateMethod=new Dictionary<TKey, Func<MachineState<TKey,TMethodRet,TMethodAgr>,TMethodAgr, TMethodRet>>();
    }
	public class Stater{
		public Stater(){
		}
        public void setState(SafeDictionary<String,int?> newState){
            var oldState = state.CopyTo();
            foreach(KeyValuePair<string,int?> pair in newState._innerObject){
				if(state.ContainsKey(pair.Key)){
					state[pair.Key]=pair.Value;
				}
				else{
					state.Add(pair.Key,pair.Value);
				}
			}
            if (BeforeSetCallback != null)
            {
                BeforeSetCallback(oldState, state);
            }
            if (AfterSetCallback!=null)
            	AfterSetCallback(state);
			if(AfterSetCallbackOnce!=null){
				AfterSetCallbackOnce(state);
            	foreach(var i in AfterSetCallbackOnce.GetInvocationList()){
					AfterSetCallbackOnce -= (AfterSet)i;
            	}
			}
        }
		public void addAfterSetCallback(AfterSet method){
			AfterSetCallback+=method;
		}
		public void addAfterSetCallbackOnce(AfterSet method){
			AfterSetCallbackOnce+=method;
		}
		public void addBeforeSetCallback(BeforeSet method){
			BeforeSetCallback+=method;
        }
		private SafeDictionary<String,int?> state=new SafeDictionary<String,int?>();
		public delegate void AfterSet(SafeDictionary<String,int?> state);
		public delegate void BeforeSet(SafeDictionary<String,int?> oldState,SafeDictionary<String,int?> appendState);
		private AfterSet AfterSetCallback;
		private AfterSet AfterSetCallbackOnce;
        private BeforeSet BeforeSetCallback;
    }

	/*
	a dictionary
	can only be modified by code in this assemble
	*/
	public class SafeDictionary<TKey,TValue>:IEnumerable{
        public SafeDictionary<TKey, TValue> CopyTo()
        {
            SafeDictionary<TKey, TValue> newObject = new SafeDictionary<TKey, TValue>();
            foreach(var item in _innerObject)
            {
                newObject.Add(item.Key, item.Value);
            }
            return newObject;
        }
        public SafeDictionary(){
            _innerObject = new Dictionary<TKey, TValue>();
        }
		public SafeDictionary(SafeDictionary<TKey,TValue> obj){
            _innerObject = new Dictionary<TKey, TValue>(obj._innerObject);
        }
		public SafeDictionary(Dictionary<TKey,TValue> obj){
            _innerObject = new Dictionary<TKey, TValue>(obj);
        }
        public IEnumerator GetEnumerator()
        {
            return _innerObject.GetEnumerator();
        }
        internal Dictionary<TKey, TValue> _innerObject;
		public TValue this[TKey key]{
			get{
                try
                {
                    var ret = _innerObject[key];
                    return ret;
                }
                catch
                {
                    return default(TValue);
                }
            }
			/*
			data modify action
			*/
			internal set{
                _innerObject[key] = value;
            }
		}
		public bool ContainsKey(TKey key){
            return _innerObject.ContainsKey(key);
        }
		/*
		data modify action
		*/
		internal void Add(TKey key,TValue value){
            _innerObject.Add(key, value);
        }
		/*
		return a new dictionary object
		*/
		public static implicit operator Dictionary<TKey,TValue>(SafeDictionary<TKey,TValue> a){
            Dictionary<TKey, TValue> newObj = new Dictionary<TKey, TValue>(a._innerObject);
            return newObj;
        }
		public static implicit operator SafeDictionary<TKey,TValue>(Dictionary<TKey,TValue> a){
            SafeDictionary<TKey, TValue> newObj = new SafeDictionary<TKey, TValue>(a);
            return newObj;
        }
    }
}