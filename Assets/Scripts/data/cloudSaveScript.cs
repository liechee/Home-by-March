using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.CloudSave;
using UnityEngine.UI;
using Unity.Services.Core;

public class cloudSaveScript : MonoBehaviour
{
    public Text status;
    public InputField inpf;

    public async void Start()
    {
        await UnityServices.InitializeAsync();
    }

    public async void SaveData()
    {
        var data = new Dictionary<string, object> { { "firstData", inpf.text } };
        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
    }


    public async void LoadData()
    {

        //    Dictionary<string ,string> serverData=  await CloudSaveService.Instance.Data.LoadAsync(new HashSet<string> { "firstData" });

        //     if (serverData.ContainsKey("firstData"))
        //     {
        //         inpf.text = serverData["firstData"];
        //     }
        //     else
        //     {
        //         print("Key not found!!");
        //     }
        var result = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "firstData" });

        if (result.TryGetValue("firstData", out var item))
        {
            inpf.text = item.Value.GetAsString();
        }
        else
        {
            print("Key not found!!");
        }

    }

    public async void DeleteKey()
    {
        await CloudSaveService.Instance.Data.Player.DeleteAsync("firstData", new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions());
    }
    public async void RetriveAllKeys()
    {
        var allData = await CloudSaveService.Instance.Data.Player.LoadAllAsync();
        List<string> allKeys = new List<string>(allData.Keys);

        for (int i = 0; i < allKeys.Count; i++)
        {
            print(allKeys[i]);
        }
    }
}