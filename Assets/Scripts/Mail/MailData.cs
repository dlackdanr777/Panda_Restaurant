using BackEnd;
using LitJson;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>우편 첨부 아이템 한 건 (GetPostList 응답의 items 배열 요소)</summary>
public class MailItem
{
    public string ItemName { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>뒤끝 서버에서 가져온 우편 한 건의 데이터 (Backend.UPost 기준)</summary>
public class MailData
{
    public PostType PostType { get; private set; }
    public string InDate { get; private set; }
    public string Title { get; private set; }
    public string Content { get; private set; }
    public string Author { get; private set; }
    public DateTime ExpirationDate { get; private set; }
    public bool IsReceived { get; private set; }
    public List<MailItem> Items { get; private set; }

    /// <summary>만료일이 지났으면 true</summary>
    public bool IsExpired => DateTime.UtcNow > ExpirationDate;

    public MailData(PostType postType, JsonData json)
    {
        PostType = postType;
        Items = new List<MailItem>();

        try
        {
            InDate  = json.ContainsKey("inDate")   ? json["inDate"].ToString()   : string.Empty;
            Title   = json.ContainsKey("title")    ? json["title"].ToString()    : string.Empty;
            Content = json.ContainsKey("content")  ? json["content"].ToString()  : string.Empty;
            Author  = json.ContainsKey("author")   ? json["author"].ToString()   : string.Empty;
            IsReceived = false;

            if (json.ContainsKey("expirationDate") &&
                DateTime.TryParse(json["expirationDate"].ToString(), out DateTime expDate))
                ExpirationDate = expDate;
            else
                ExpirationDate = DateTime.MaxValue;

            // GetPostList 응답의 items 배열 파싱
            if (json.ContainsKey("items") && json["items"].IsArray)
            {
                for (int i = 0; i < json["items"].Count; i++)
                {
                    JsonData itemJson = json["items"][i];
                    if (!itemJson.ContainsKey("item")) continue;

                    MailItem mi = new MailItem();
                    if (itemJson["item"].ContainsKey("itemName"))
                        mi.ItemName = itemJson["item"]["itemName"].ToString();
                    if (itemJson.ContainsKey("itemCount") &&
                        int.TryParse(itemJson["itemCount"].ToString(), out int cnt))
                        mi.ItemCount = cnt;

                    if (!string.IsNullOrEmpty(mi.ItemName))
                        Items.Add(mi);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MailData] JSON 파싱 오류: {ex.Message}");
        }
    }

    public void SetReceived() => IsReceived = true;
}
