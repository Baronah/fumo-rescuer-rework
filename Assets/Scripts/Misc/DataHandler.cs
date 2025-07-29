using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts
{
    public class DataHandler : MonoBehaviour
    {
        public static DataHandler Instance;

        public Dictionary<string, AsyncOperationHandle<UnityEngine.GameObject>> loadedAssets = new Dictionary<string, AsyncOperationHandle<UnityEngine.GameObject>>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public AsyncOperationHandle<UnityEngine.GameObject> LoadAddressable<GameObject>(AssetReference assetReference)
        {
            string key = assetReference.AssetGUID.ToString();
            if (loadedAssets.ContainsKey(key))
            {
                return loadedAssets[key];
            }
            else
            {
                AsyncOperationHandle<UnityEngine.GameObject> handle = assetReference.LoadAssetAsync<UnityEngine.GameObject>();
                loadedAssets[key] = handle;
                return handle;
            }
        }

        public bool IsAssetLoaded(string assetKey)
        {
            return loadedAssets.ContainsKey(assetKey);
        }

        public void ReleaseAsset(string assetKey)
        {
            if (loadedAssets.ContainsKey(assetKey))
            {
                Addressables.Release(loadedAssets[assetKey]);
                loadedAssets.Remove(assetKey);
            }
        }

        public void ReleaseEverything()
        {
            for (int i = 0; i < loadedAssets.Count; ++i)
            {
                var item = loadedAssets.ElementAt(i);
                var key = loadedAssets[item.Key];
                Addressables.Release(key);
                loadedAssets.Remove(item.Key);
            }
        }
    }
}
