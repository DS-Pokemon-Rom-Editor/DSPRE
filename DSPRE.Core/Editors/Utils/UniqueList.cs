using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    public class UniqueList<T>
    {
        private readonly List<T> _list = new List<T>();
        private readonly HashSet<T> _set = new HashSet<T>();

        public UniqueList(int capacity)
        {
            _list = new List<T>(capacity);
            _set = new HashSet<T>(capacity);
        }

        public UniqueList() { }

        public void Add(T item)
        {
            if (!_set.Contains(item))
            {
                _list.Add(item);
                _set.Add(item);
            }
        }

        public bool Contains(T item)
        {
            return _set.Contains(item);
        }

        public void Clear()
        {
            _list.Clear();
            _set.Clear();
        }

        public bool Remove(T item)
        {
            if (_set.Contains(item))
            {
                _list.Remove(item);
                _set.Remove(item);
                return true;
            }
            return false;
        }
        public bool RemoveAt(int index)
        {
            if (index >= 0 && index < _list.Count)
            {
                T itemToRemove = _list[index];
                _list.RemoveAt(index);
                _set.Remove(itemToRemove);
                return true;
            }
            return false;
        }

        public bool Insert(int index, T item)
        {
            if (_set.Contains(item))
            {
                // An item already in the set is moved to the new index.
                int oldIndex = _list.FindIndex(x => x.Equals(item));
                if (oldIndex != index)
                {
                    _list.Move(oldIndex, index);
                }
                return false;
            }
            else
            {
                _list.Insert(index, item);
                _set.Add(item);
                return true;
            }
        }


        public T Find(Predicate<T> match)
        {
            return _list.Find(match);
        }
        public int FindIndex(Predicate<T> match)
        {
            return _list.FindIndex(match);
        }
        public void Sort()
        {
            _list.Sort();
        }
        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public T this[int index]
        {
            get { return _list[index]; }

            set
            {
                if (_set.Contains(value))
                {
                    int oldIndex = _list.FindIndex(x => x.Equals(value));
                    if (index == oldIndex)
                    {

                        return;
                    }


                    _list.Move(oldIndex, index);
                }
                else
                {

                    _list[index] = value;
                    _set.Add(value);
                }
            }
        }

        public int Count => _list.Count;
    }

}
