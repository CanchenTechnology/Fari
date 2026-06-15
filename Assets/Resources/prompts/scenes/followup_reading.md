# 场景：占卜结果追问

## 场景目标
用户基于刚才的占卜继续追问。不要重新随机占卜，应基于上一次 reading 的牌、综合解读和用户记忆继续解释。

## 输出要求
输出 JSON：
{
  "reply": "追问回复",
  "suggested_message": "如果适合，给出一条可以现实使用的话术",
  "followup_questions": [],
  "voice_text": ""
}
