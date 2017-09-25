using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stellariview {

    public class GallerySet {
        private bool sorted = false;
        private ImageContainer cur;
        private int curIndex = -1;

        public List<ImageContainer> entriesBase = new List<ImageContainer>();
        public List<ImageContainer> entriesShuffled;

        public GallerySet() { }

        public List<ImageContainer> Entries {
            get {
                if (entriesShuffled != null) return entriesShuffled;
                return entriesBase;
            }
        }

        public ImageContainer Current {
            get {
                if (cur == null) {
                    if (Entries.Count > 0) cur = Entries[0];
                    updateIndex();
                }
                return cur;
            }
            set {
                cur = value;
                updateIndex();
            }
        }

        private int mod(int x, int m) {
            return (x % m + m) % m;
        }

        private void updateIndex() {
            curIndex = Entries.IndexOf(Current);
        }
        public int CurrentIndex { get { return curIndex; } }

        public ImageContainer OffsetCurrent(int offset) {
            if (Entries.Count == 0) return null; // todo: return something more sensible if empty?
            if (curIndex < 0) return Entries[0]; // fall back on first entry if something weird happens
            return Entries[mod(curIndex + offset, Entries.Count)];
        }

        public GallerySet ScrollCurrent(int offset, bool ignoreShuffle = false) {
            if (!ignoreShuffle || !IsShuffled) {
                Current = OffsetCurrent(offset);
            } else {
                // slightly more roundabout, scroll by unshuffled ID
                Current = entriesBase[mod(entriesBase.IndexOf(Current) + offset, entriesBase.Count)];
            }
            return this;
        }

        public GallerySet Add(ImageContainer img, bool sort = true) {
            entriesBase.Add(img);
            if (entriesShuffled != null) {
                Random rand = new Random();
                entriesShuffled.Insert(rand.Next(entriesShuffled.Count), img);
            }
            sorted = false;
            if (sort) Sort();
            return this;
        }

        public GallerySet Remove(ImageContainer img) {
            if (img == Current) ScrollCurrent(1); // displace if removing selected image
            entriesBase.Remove(img);
            if (entriesShuffled != null) entriesShuffled.Remove(img);
            updateIndex();

            return this;
        }

        public GallerySet Sort() {
            if (sorted) return this; // no need to re-sort

            NumericComparer nc = new NumericComparer();
            entriesBase.Sort((ImageContainer e1, ImageContainer e2) => { return nc.Compare(e1.sourcePath.FileName, e2.sourcePath.FileName); }); // sort by filename

            sorted = true;
            updateIndex();
            return this;
        }

        public GallerySet Shuffle(bool shuffled) {
            if (!shuffled) {
                entriesShuffled = null; // discard
            }
            else {
                Random rand = new Random();

                entriesShuffled = new List<ImageContainer>();
                foreach (ImageContainer entry in entriesBase) {
                    entriesShuffled.Insert(rand.Next(entriesShuffled.Count), entry);
                }
            }
            updateIndex();
            return this;
        }
        public GallerySet Shuffle() { return Shuffle(entriesShuffled == null); }
        public bool IsShuffled { get { return entriesShuffled != null; } }
    }
}
