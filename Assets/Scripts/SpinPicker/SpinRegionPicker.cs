using System;
using SuperScrollView;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class SpinRegionPicker : MonoBehaviour
{
	private class ProvinceGroup
	{
		public readonly string Province;
		public readonly string[] Cities;

		public ProvinceGroup(string province, params string[] cities)
		{
			Province = province;
			Cities = cities;
		}
	}

	private class CountryGroup
	{
		public readonly string Country;
		public readonly ProvinceGroup[] Provinces;

		public CountryGroup(string country, params ProvinceGroup[] provinces)
		{
			Country = country;
			Provinces = provinces;
		}
	}

	private static readonly CountryGroup[] RegionGroups =
	{
		new CountryGroup("中国",
			new ProvinceGroup("北京", "东城区", "西城区", "朝阳区", "海淀区", "丰台区", "石景山区", "通州区", "昌平区", "大兴区", "顺义区"),
			new ProvinceGroup("上海", "黄浦区", "徐汇区", "长宁区", "静安区", "普陀区", "虹口区", "浦东新区", "闵行区", "宝山区", "嘉定区"),
			new ProvinceGroup("天津", "和平区", "河东区", "河西区", "南开区", "河北区", "红桥区", "滨海新区", "津南区", "北辰区"),
			new ProvinceGroup("重庆", "渝中区", "江北区", "南岸区", "沙坪坝区", "九龙坡区", "渝北区", "巴南区", "北碚区", "万州区"),
			new ProvinceGroup("广东", "广州", "深圳", "珠海", "佛山", "东莞", "中山", "惠州", "汕头"),
			new ProvinceGroup("浙江", "杭州", "宁波", "温州", "绍兴", "嘉兴", "金华", "台州"),
			new ProvinceGroup("江苏", "南京", "苏州", "无锡", "常州", "南通", "徐州", "扬州"),
			new ProvinceGroup("四川", "成都", "绵阳", "德阳", "乐山", "宜宾", "南充"),
			new ProvinceGroup("湖北", "武汉", "宜昌", "襄阳", "荆州", "黄石"),
			new ProvinceGroup("陕西", "西安", "咸阳", "宝鸡", "渭南", "延安"),
			new ProvinceGroup("山东", "济南", "青岛", "烟台", "潍坊", "威海", "临沂"),
			new ProvinceGroup("福建", "福州", "厦门", "泉州", "漳州", "莆田"),
			new ProvinceGroup("湖南", "长沙", "株洲", "湘潭", "衡阳", "岳阳"),
			new ProvinceGroup("河南", "郑州", "洛阳", "开封", "南阳", "许昌"),
			new ProvinceGroup("辽宁", "沈阳", "大连", "鞍山", "锦州"),
			new ProvinceGroup("吉林", "长春", "吉林", "延吉"),
			new ProvinceGroup("黑龙江", "哈尔滨", "齐齐哈尔", "大庆"),
			new ProvinceGroup("安徽", "合肥", "芜湖", "蚌埠", "黄山"),
			new ProvinceGroup("江西", "南昌", "九江", "赣州", "景德镇"),
			new ProvinceGroup("云南", "昆明", "大理", "丽江", "西双版纳"),
			new ProvinceGroup("贵州", "贵阳", "遵义", "安顺"),
			new ProvinceGroup("广西", "南宁", "桂林", "柳州", "北海"),
			new ProvinceGroup("海南", "海口", "三亚"),
			new ProvinceGroup("河北", "石家庄", "唐山", "秦皇岛", "保定"),
			new ProvinceGroup("山西", "太原", "大同", "临汾"),
			new ProvinceGroup("内蒙古", "呼和浩特", "包头", "赤峰"),
			new ProvinceGroup("甘肃", "兰州", "敦煌", "天水"),
			new ProvinceGroup("青海", "西宁", "格尔木"),
			new ProvinceGroup("宁夏", "银川", "石嘴山"),
			new ProvinceGroup("新疆", "乌鲁木齐", "喀什", "伊犁", "吐鲁番"),
			new ProvinceGroup("西藏", "拉萨", "日喀则"),
			new ProvinceGroup("香港", "中西区", "湾仔区", "东区", "南区", "油尖旺区", "深水埗区", "九龙城区", "沙田区", "荃湾区"),
			new ProvinceGroup("澳门", "澳门半岛", "氹仔", "路环", "路氹"),
			new ProvinceGroup("台湾", "台北", "高雄", "台中", "台南")),
		new CountryGroup("美国",
			new ProvinceGroup("加利福尼亚州", "洛杉矶", "旧金山", "圣迭戈", "圣何塞", "萨克拉门托"),
			new ProvinceGroup("纽约州", "纽约", "布法罗", "罗切斯特", "奥尔巴尼"),
			new ProvinceGroup("华盛顿州", "西雅图", "贝尔维尤", "塔科马", "斯波坎"),
			new ProvinceGroup("德克萨斯州", "休斯敦", "达拉斯", "奥斯汀", "圣安东尼奥"),
			new ProvinceGroup("佛罗里达州", "迈阿密", "奥兰多", "坦帕", "杰克逊维尔"),
			new ProvinceGroup("伊利诺伊州", "芝加哥", "香槟", "斯普林菲尔德"),
			new ProvinceGroup("马萨诸塞州", "波士顿", "剑桥", "伍斯特"),
			new ProvinceGroup("新泽西州", "纽瓦克", "泽西城", "普林斯顿"),
			new ProvinceGroup("佐治亚州", "亚特兰大", "萨凡纳"),
			new ProvinceGroup("宾夕法尼亚州", "费城", "匹兹堡", "哈里斯堡")),
		new CountryGroup("加拿大",
			new ProvinceGroup("安大略省", "多伦多", "渥太华", "滑铁卢", "伦敦"),
			new ProvinceGroup("不列颠哥伦比亚省", "温哥华", "维多利亚", "本拿比", "列治文"),
			new ProvinceGroup("魁北克省", "蒙特利尔", "魁北克城"),
			new ProvinceGroup("艾伯塔省", "卡尔加里", "埃德蒙顿"),
			new ProvinceGroup("曼尼托巴省", "温尼伯")),
		new CountryGroup("英国",
			new ProvinceGroup("英格兰", "伦敦", "曼彻斯特", "伯明翰", "利物浦", "布里斯托"),
			new ProvinceGroup("苏格兰", "爱丁堡", "格拉斯哥", "阿伯丁"),
			new ProvinceGroup("威尔士", "卡迪夫", "斯旺西"),
			new ProvinceGroup("北爱尔兰", "贝尔法斯特")),
		new CountryGroup("澳大利亚",
			new ProvinceGroup("新南威尔士州", "悉尼", "纽卡斯尔", "伍伦贡"),
			new ProvinceGroup("维多利亚州", "墨尔本", "吉朗"),
			new ProvinceGroup("昆士兰州", "布里斯班", "黄金海岸", "凯恩斯"),
			new ProvinceGroup("西澳大利亚州", "珀斯", "弗里曼特尔"),
			new ProvinceGroup("南澳大利亚州", "阿德莱德"),
			new ProvinceGroup("首都领地", "堪培拉")),
		new CountryGroup("日本",
			new ProvinceGroup("东京都", "东京"),
			new ProvinceGroup("大阪府", "大阪", "堺"),
			new ProvinceGroup("京都府", "京都"),
			new ProvinceGroup("神奈川县", "横滨", "川崎", "镰仓"),
			new ProvinceGroup("北海道", "札幌", "函馆"),
			new ProvinceGroup("福冈县", "福冈", "北九州"),
			new ProvinceGroup("冲绳县", "那霸")),
		new CountryGroup("韩国",
			new ProvinceGroup("首尔特别市", "首尔"),
			new ProvinceGroup("釜山广域市", "釜山"),
			new ProvinceGroup("仁川广域市", "仁川"),
			new ProvinceGroup("京畿道", "水原", "城南", "高阳"),
			new ProvinceGroup("济州特别自治道", "济州")),
		new CountryGroup("新加坡",
			new ProvinceGroup("新加坡", "新加坡")),
		new CountryGroup("德国",
			new ProvinceGroup("柏林", "柏林"),
			new ProvinceGroup("巴伐利亚州", "慕尼黑", "纽伦堡"),
			new ProvinceGroup("汉堡", "汉堡"),
			new ProvinceGroup("黑森州", "法兰克福", "威斯巴登"),
			new ProvinceGroup("北莱茵-威斯特法伦州", "科隆", "杜塞尔多夫", "多特蒙德")),
		new CountryGroup("法国",
			new ProvinceGroup("法兰西岛", "巴黎", "凡尔赛"),
			new ProvinceGroup("奥弗涅-罗讷-阿尔卑斯", "里昂", "格勒诺布尔"),
			new ProvinceGroup("普罗旺斯-阿尔卑斯-蓝色海岸", "马赛", "尼斯", "戛纳"),
			new ProvinceGroup("奥克西塔尼", "图卢兹", "蒙彼利埃"),
			new ProvinceGroup("新阿基坦", "波尔多"))
	};

	public LoopListView2 mLoopListViewCountry;
	[FormerlySerializedAs("mLoopListViewRegion")]
	public LoopListView2 mLoopListViewProvince;
	public LoopListView2 mLoopListViewCity;
	public Color mColorReserved = new Color(0.63f, 0.60f, 0.66f, 1f);
	public Color mColorSelected = new Color(1f, 0.84f, 0.48f, 1f);
	public Text CurSelect;
	public Button ConfirmButton;

	private int mCurSelectedCountryIndex;
	private int mCurSelectedProvinceIndex;
	private int mCurSelectedCityIndex;
	private bool mInitialized;
	private Action<string> mConfirmCallback;

	public string CurSelectedCountry => CurrentCountry.Country;
	public string CurSelectedProvince => CurrentProvince.Province;
	public string CurSelectedCity => CurrentCities[Mathf.Clamp(mCurSelectedCityIndex, 0, CurrentCities.Length - 1)];
	public string SelectedValue => $"{CurSelectedCountry} · {CurSelectedProvince} · {CurSelectedCity}";

	private CountryGroup CurrentCountry => RegionGroups[Mathf.Clamp(mCurSelectedCountryIndex, 0, RegionGroups.Length - 1)];
	private ProvinceGroup[] CurrentProvinces => CurrentCountry.Provinces;
	private ProvinceGroup CurrentProvince => CurrentProvinces[Mathf.Clamp(mCurSelectedProvinceIndex, 0, CurrentProvinces.Length - 1)];
	private string[] CurrentCities => CurrentProvince.Cities;

	public void Show(string initialValue, Action<string> confirmCallback)
	{
		gameObject.SetActive(true);
		BindReferences();
		SelectInitialRegion(initialValue);
		mConfirmCallback = confirmCallback;

		EnsureInitialized();
		MoveToCurrentRegion();
		UpdateCurSelect();
	}

	private void Awake()
	{
		BindReferences();
	}

	private void BindReferences()
	{
		if (mLoopListViewCountry == null)
		{
			Transform item = transform.Find("ScrollViewCountry");
			if (item != null) mLoopListViewCountry = item.GetComponent<LoopListView2>();
		}
		if (mLoopListViewProvince == null)
		{
			Transform item = transform.Find("ScrollViewProvince");
			if (item == null) item = transform.Find("ScrollViewRegion");
			if (item != null) mLoopListViewProvince = item.GetComponent<LoopListView2>();
		}
		if (mLoopListViewCity == null)
		{
			Transform item = transform.Find("ScrollViewCity");
			if (item != null) mLoopListViewCity = item.GetComponent<LoopListView2>();
		}
		if (CurSelect == null)
		{
			Transform item = transform.Find("CurSelect");
			if (item != null) CurSelect = item.GetComponent<Text>();
		}
		if (ConfirmButton == null)
		{
			Transform item = transform.Find("confirmBtn");
			if (item == null) item = transform.Find("[Button]Confirm");
			if (item != null) ConfirmButton = item.GetComponent<Button>();
		}
	}

	private void EnsureInitialized()
	{
		if (mInitialized) return;
		if (mLoopListViewCountry == null || mLoopListViewProvince == null || mLoopListViewCity == null)
		{
			Debug.LogError("[SpinRegionPicker] Country, province or city LoopListView2 reference is missing.");
			return;
		}

		mLoopListViewCountry.mOnSnapNearestChanged = OnCountrySnapTargetChanged;
		mLoopListViewCountry.mOnSnapItemFinished = OnCountrySnapTargetFinished;
		mLoopListViewProvince.mOnSnapNearestChanged = OnProvinceSnapTargetChanged;
		mLoopListViewProvince.mOnSnapItemFinished = OnProvinceSnapTargetFinished;
		mLoopListViewCity.mOnSnapNearestChanged = OnCitySnapTargetChanged;

		mLoopListViewCountry.InitListView(-1, OnGetItemByIndexForCountry);
		mLoopListViewProvince.InitListView(-1, OnGetItemByIndexForProvince);
		mLoopListViewCity.InitListView(-1, OnGetItemByIndexForCity);

		if (ConfirmButton != null)
		{
			ConfirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
			ConfirmButton.onClick.AddListener(OnConfirmButtonClicked);
		}

		mInitialized = true;
	}

	private void SelectInitialRegion(string value)
	{
		mCurSelectedCountryIndex = 0;
		mCurSelectedProvinceIndex = 0;
		mCurSelectedCityIndex = 0;
		if (string.IsNullOrWhiteSpace(value)) return;

		string normalized = value.Trim();
		string[] tokens = normalized.Split(new[] { '·', ',', '，', '/', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

		for (int i = 0; i < RegionGroups.Length; i++)
		{
			if (Matches(normalized, tokens, RegionGroups[i].Country))
			{
				mCurSelectedCountryIndex = i;
				break;
			}
		}

		for (int i = 0; i < CurrentProvinces.Length; i++)
		{
			if (Matches(normalized, tokens, CurrentProvinces[i].Province))
			{
				mCurSelectedProvinceIndex = i;
				break;
			}
		}

		if (TryFindCity(normalized, tokens, out int provinceIndex, out int cityIndex))
		{
			mCurSelectedProvinceIndex = provinceIndex;
			mCurSelectedCityIndex = cityIndex;
		}
	}

	private bool Matches(string normalized, string[] tokens, string target)
	{
		if (string.IsNullOrWhiteSpace(target)) return false;
		for (int i = 0; i < tokens.Length; i++)
		{
			if (string.Equals(tokens[i].Trim(), target, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return normalized.Contains(target);
	}

	private bool TryFindCity(string normalized, string[] tokens, out int provinceIndex, out int cityIndex)
	{
		provinceIndex = 0;
		cityIndex = 0;
		for (int i = 0; i < CurrentProvinces.Length; i++)
		{
			string[] cities = CurrentProvinces[i].Cities;
			for (int j = 0; j < cities.Length; j++)
			{
				if (Matches(normalized, tokens, cities[j]))
				{
					provinceIndex = i;
					cityIndex = j;
					return true;
				}
			}
		}
		return false;
	}

	private void MoveToCurrentRegion()
	{
		MoveListToIndex(mLoopListViewCountry, mCurSelectedCountryIndex);
		MoveListToIndex(mLoopListViewProvince, mCurSelectedProvinceIndex);
		MoveListToIndex(mLoopListViewCity, mCurSelectedCityIndex);
	}

	private void MoveListToIndex(LoopListView2 listView, int index)
	{
		if (listView == null) return;
		listView.MovePanelToItemIndex(index - 1, 0);
		listView.FinishSnapImmediately();
	}

	private void UpdateCurSelect()
	{
		if (CurSelect != null)
		{
			CurSelect.text = SelectedValue;
		}
	}

	private LoopListViewItem2 OnGetItemByIndexForCountry(LoopListView2 listView, int index)
	{
		int valueIndex = NormalizeIndex(index, RegionGroups.Length);
		return CreateTextItem(listView, valueIndex, RegionGroups[valueIndex].Country);
	}

	private LoopListViewItem2 OnGetItemByIndexForProvince(LoopListView2 listView, int index)
	{
		ProvinceGroup[] provinces = CurrentProvinces;
		int valueIndex = NormalizeIndex(index, provinces.Length);
		return CreateTextItem(listView, valueIndex, provinces[valueIndex].Province);
	}

	private LoopListViewItem2 OnGetItemByIndexForCity(LoopListView2 listView, int index)
	{
		string[] cities = CurrentCities;
		int valueIndex = NormalizeIndex(index, cities.Length);
		return CreateTextItem(listView, valueIndex, cities[valueIndex]);
	}

	private LoopListViewItem2 CreateTextItem(LoopListView2 listView, int valueIndex, string label)
	{
		LoopListViewItem2 item = listView.NewListViewItem("ItemPrefab");
		if (item == null) return null;
		SpinPickerItem itemScript = item.GetComponent<SpinPickerItem>();
		if (itemScript == null) return item;
		if (item.IsInitHandlerCalled == false)
		{
			item.IsInitHandlerCalled = true;
			itemScript.Init();
		}

		itemScript.Value = valueIndex;
		Text itemText = EnsureItemText(itemScript, item.transform);
		if (itemText != null)
		{
			itemText.text = label;
		}
		return item;
	}

	private Text EnsureItemText(SpinPickerItem itemScript, Transform itemTransform)
	{
		if (itemScript.mText != null)
		{
			return itemScript.mText;
		}

		itemScript.mText = itemTransform.GetComponentInChildren<Text>(true);
		if (itemScript.mText != null)
		{
			return itemScript.mText;
		}

		GameObject textObject = new GameObject("TextName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
		textObject.transform.SetParent(itemTransform, false);
		RectTransform textRect = textObject.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;

		Text text = textObject.GetComponent<Text>();
		text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		text.fontSize = 34;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = mColorReserved;
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		itemScript.mText = text;
		return itemScript.mText;
	}

	private int NormalizeIndex(int index, int count)
	{
		if (count <= 0) return 0;
		if (index >= 0) return index % count;
		return count + ((index + 1) % count) - 1;
	}

	private void OnCountrySnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (!TryUpdateSelectedIndex(listView, item, out int valueIndex)) return;
		if (mCurSelectedCountryIndex == valueIndex) return;

		mCurSelectedCountryIndex = valueIndex;
		mCurSelectedProvinceIndex = 0;
		mCurSelectedCityIndex = 0;
		UpdateCurSelect();
	}

	private void OnCountrySnapTargetFinished(LoopListView2 listView, LoopListViewItem2 item)
	{
		RefreshProvinceColumn();
	}

	private void OnProvinceSnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (!TryUpdateSelectedIndex(listView, item, out int valueIndex)) return;
		if (mCurSelectedProvinceIndex == valueIndex) return;

		mCurSelectedProvinceIndex = valueIndex;
		mCurSelectedCityIndex = 0;
		UpdateCurSelect();
	}

	private void OnProvinceSnapTargetFinished(LoopListView2 listView, LoopListViewItem2 item)
	{
		RefreshCityColumn();
	}

	private void OnCitySnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (!TryUpdateSelectedIndex(listView, item, out int valueIndex)) return;
		mCurSelectedCityIndex = valueIndex;
		UpdateCurSelect();
	}

	private bool TryUpdateSelectedIndex(LoopListView2 listView, LoopListViewItem2 item, out int valueIndex)
	{
		valueIndex = 0;
		int index = listView.GetIndexInShownItemList(item);
		if (index < 0) return false;
		SpinPickerItem itemScript = item.GetComponent<SpinPickerItem>();
		if (itemScript == null) return false;
		valueIndex = itemScript.Value;
		OnListViewSnapTargetChanged(listView, index);
		return true;
	}

	private void RefreshProvinceColumn()
	{
		if (mLoopListViewProvince == null || !mLoopListViewProvince.ListViewInited) return;
		mCurSelectedProvinceIndex = Mathf.Clamp(mCurSelectedProvinceIndex, 0, CurrentProvinces.Length - 1);
		mCurSelectedCityIndex = 0;
		mLoopListViewProvince.RefreshAllShownItem();
		MoveListToIndex(mLoopListViewProvince, mCurSelectedProvinceIndex);
		RefreshCityColumn();
		UpdateCurSelect();
	}

	private void RefreshCityColumn()
	{
		if (mLoopListViewCity == null || !mLoopListViewCity.ListViewInited) return;
		mCurSelectedCityIndex = Mathf.Clamp(mCurSelectedCityIndex, 0, CurrentCities.Length - 1);
		mLoopListViewCity.RefreshAllShownItem();
		MoveListToIndex(mLoopListViewCity, mCurSelectedCityIndex);
		UpdateCurSelect();
	}

	private void OnListViewSnapTargetChanged(LoopListView2 listView, int targetIndex)
	{
		int count = listView.ShownItemCount;
		for (int i = 0; i < count; ++i)
		{
			LoopListViewItem2 item = listView.GetShownItemByIndex(i);
			SpinPickerItem itemScript = item != null ? item.GetComponent<SpinPickerItem>() : null;
			if (itemScript != null && itemScript.mText != null)
			{
				itemScript.mText.color = i == targetIndex ? mColorSelected : mColorReserved;
			}
		}
	}

	private void OnConfirmButtonClicked()
	{
		mConfirmCallback?.Invoke(SelectedValue);
	}
}
