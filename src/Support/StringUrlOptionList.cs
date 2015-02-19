using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Efocus.Sitecore.LuceneWebSearch.Support
{
    public class StringUrlOptionList : IList<String>, IList
    {
        private readonly List<UrlCrawlerOptions> _urlOptions;

        private IEnumerable<string> UrlStringOptions
        {
            get { return _urlOptions.Select(it => it.Url); }
        }

        public StringUrlOptionList(List<UrlCrawlerOptions> urlOptions)
        {
            // TODO: Complete member initialization
            this._urlOptions = urlOptions;
        }

        public int IndexOf(string item)
        {
            return _urlOptions.FindIndex(it => it.Url == item);
        }

        public void Insert(int index, string item)
        {
            _urlOptions.Insert(index, new UrlCrawlerOptions(item));
        }

        public void RemoveAt(int index)
        {
            _urlOptions.RemoveAt(index);
        }

        public string this[int index]
        {
            get { return _urlOptions[index].Url; }
            set { _urlOptions[index].Url = value; }
        }

        public void Add(string item)
        {
            _urlOptions.Add(new UrlCrawlerOptions(item));
        }

        public void Clear()
        {
            _urlOptions.Clear();
        }

        public bool Contains(string item)
        {
            return IndexOf(item) != -1;
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            UrlStringOptions.ToList().CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _urlOptions.Count; }
        }

        public bool IsReadOnly
        {
            get { return ((IList)_urlOptions).IsReadOnly; }
        }

        public bool Remove(string item)
        {
            var index = IndexOf(item);
            if (index != -1)
            {
                _urlOptions.RemoveAt(index);
                return true;
            }
            return false;
        }

        public IEnumerator<string> GetEnumerator()
        {
            return UrlStringOptions.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public int Add(object value)
        {
            var valueAsString = value as String;
            this.Add(valueAsString);
            return IndexOf(valueAsString);
        }

        public bool Contains(object value)
        {
            var valueAsString = value as String;
            return this.Contains(valueAsString);
        }

        public int IndexOf(object value)
        {
            var valueAsString = value as String;
            return this.IndexOf(valueAsString);
        }

        public void Insert(int index, object value)
        {
            var valueAsString = value as String;
            this.Insert(index, valueAsString);
        }

        public bool IsFixedSize
        {
            get { return false; }
        }

        public void Remove(object value)
        {
            var valueAsString = value as String;
            this.Remove(valueAsString);
        }

        object IList.this[int index]
        {
            get
            {
                return this[index];
            }
            set
            {
                var valueAsString = value as String;
                this[index] = valueAsString;
            }
        }

        public void CopyTo(Array array, int index)
        {
            ((IList)UrlStringOptions.ToList()).CopyTo(array, index);
        }

        public bool IsSynchronized
        {
            get { return ((IList)UrlStringOptions).IsSynchronized; }
        }

        public object SyncRoot
        {
            get { return ((IList)UrlStringOptions).SyncRoot; }
        }
    }
}
