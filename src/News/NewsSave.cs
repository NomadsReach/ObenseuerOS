using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    [Serializable]
    public class NewsSave
    {
        public int Seed;
        public List<NewsEdition> Editions = new List<NewsEdition>();
    }

    [Serializable]
    public class NewsEdition
    {
        public int Day;
        public string Weekday;
        public List<NewsStory> Stories = new List<NewsStory>();
    }

    [Serializable]
    public class NewsStory
    {
        public string Section;
        public string Headline;
        public string Body;
        public List<NewsRow> Rows = new List<NewsRow>();
        public List<int> Nums = new List<int>();
    }

    [Serializable]
    public class NewsRow
    {
        public string Label;
        public float Value;
        public int Dec;
        public string Unit;
    }
}
