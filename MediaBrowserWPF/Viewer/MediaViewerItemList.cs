using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;
using System.Windows.Media.Imaging;
using MediaBrowser4;

namespace MediaBrowserWPF.Viewer
{
    public class MediaViewerItemList
    {
        public MediaItem LastSelectedMediaItem;
        private List<MediaItem> itemList;
        private int selectedMediaItemIndex = 0;
        private List<Variation> variationList;
        private int selectedVariationIndex = 0;
        public enum VariationTypeEnum { NONE, SAME_NAME, ALL };

        public event EventHandler<EventArgs> OnSelectedItemChanged;

        public MediaViewerItemList(List<MediaItem> mediaItemlist, MediaItem selectedMediaItem)
        {
            this.itemList = mediaItemlist;
            this.SelectedMediaItem = selectedMediaItem;

            if (this.selectedMediaItemIndex < 0)
                this.selectedMediaItemIndex = 0;
        }

        public bool ShowDeleted { get; set; }
        public VariationTypeEnum VariationType { get; set; }

        public void ChangeVariationType(VariationTypeEnum variationType)
        {
            if (this.VariationType != variationType && variationType != VariationTypeEnum.NONE)
            {
                this.VariationType = variationType;
                this.variationList = this.GetVariations(this.SelectedMediaItem);
                this.selectedVariationIndex = this.variationList.FindIndex(x => x.Id == this.SelectedMediaItem.VariationId);
            }

            this.VariationType = variationType;
        }

        public void SetVariation(Variation variation)
        {
            this.SetVariationList(this.variationList.FindIndex(x => x.Position == variation.Position));
            if (this.OnSelectedItemChanged != null)
            {
                this.OnSelectedItemChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public int SelectedMediaItemIndex
        {
            get
            {
                return this.selectedMediaItemIndex;
            }
        }

        public int RemoveMediaItem()
        {
            if (this.itemList.Count == 0)
                return -1;

            this.selectedMediaItemIndex--;

            if (this.selectedMediaItemIndex < 0)
                this.selectedMediaItemIndex = 0;

            return this.selectedMediaItemIndex;
        }

        public void RenameVariation(Variation variation)
        {
            Variation v = this.variationList.FirstOrDefault(x => x.Id == variation.Id);
            if (v != null)
            {
                v.Name = variation.Name;
            }
        }

        public string VariationName
        {
            get
            {
                if (this.VariationType != VariationTypeEnum.NONE && this.variationList != null)
                {
                    return this.variationList[this.selectedVariationIndex].Name;
                }
                else
                {
                    return String.Empty;
                }
            }
        }

        public string MinorVersion
        {
            get
            {
                if (this.VariationType != VariationTypeEnum.NONE && this.variationList != null && this.variationList.Count > 1)
                {
                    MediaItem mItem = this.itemList[this.selectedMediaItemIndex];
                    if (this.variationList.Count > this.selectedVariationIndex)
                    {
                        return "." + this.variationList[this.selectedVariationIndex].Position
                            + (mItem.VariationId == mItem.VariationIdDefault ? "*" : String.Empty);
                    }
                    else
                    {
                        return String.Empty;
                    }
                }
                else
                {
                    return String.Empty;
                }
            }
        }

        public MediaItem SelectedMediaItem
        {
            get
            {
                if (this.itemList.Count <= this.selectedMediaItemIndex || this.selectedMediaItemIndex < 0)
                    return null;

                MediaItem mItem = this.itemList[this.selectedMediaItemIndex];
                if (this.VariationType != VariationTypeEnum.NONE)
                {
                    if (this.variationList == null)
                    {
                        this.variationList = this.GetVariations(mItem);
                        this.selectedVariationIndex = 0;
                        mItem.ChangeVariation(this.variationList[this.selectedVariationIndex]);
                    }
                }
                else
                {
                    mItem.ResetVariation();
                }

                return mItem;
            }

            set
            {
                int a = this.itemList.FindIndex(item => item == value);
                if (a >= 0)
                {
                    this.selectedMediaItemIndex = a;

                    if (this.OnSelectedItemChanged != null)
                    {
                        this.OnSelectedItemChanged.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public void StepNext(int stepCount)
        {
            this.LastSelectedMediaItem = SelectedMediaItem;
            int oldIndex = this.selectedMediaItemIndex;

            if (this.VariationType != VariationTypeEnum.NONE && this.variationList == null)
            {
                this.variationList = this.GetVariations(this.SelectedMediaItem);
                this.selectedVariationIndex = 0;
            }

            do
            {
                if (stepCount == 0)
                {
                    this.SetVariationList(0);
                }
                else if (stepCount == 1)
                {
                    if (this.VariationType != VariationTypeEnum.NONE && this.selectedVariationIndex + 1 < this.variationList.Count
                        && (this.ShowDeleted || !this.SelectedMediaItem.IsDeleted))
                    {
                        this.selectedVariationIndex++;
                        this.SelectedMediaItem.ChangeVariation(this.variationList[this.selectedVariationIndex]);
                    }
                    else
                    {
                        if (this.selectedMediaItemIndex >= this.itemList.Count - 1)
                        {
                            this.selectedMediaItemIndex = 0;
                        }
                        else
                        {
                            this.selectedMediaItemIndex++;
                        }

                        this.SetVariationList(0);
                    }
                }
                else if (stepCount == -1)
                {
                    if (this.VariationType != VariationTypeEnum.NONE && this.selectedVariationIndex > 0
                        && (this.ShowDeleted || !this.SelectedMediaItem.IsDeleted))
                    {
                        this.selectedVariationIndex--;
                        this.SelectedMediaItem.ChangeVariation(this.variationList[this.selectedVariationIndex]);
                    }
                    else
                    {
                        if (this.selectedMediaItemIndex == 0)
                        {
                            this.selectedMediaItemIndex = this.itemList.Count - 1;
                        }
                        else
                        {
                            this.selectedMediaItemIndex--;
                        }

                        this.SetVariationList(-1);
                    }
                }
                else
                {
                    throw new Exception("Not implemented");
                }
            } while (oldIndex != this.selectedMediaItemIndex && this.itemList[this.selectedMediaItemIndex].IsDeleted && !this.ShowDeleted);

            if (this.OnSelectedItemChanged != null)
            {
                this.OnSelectedItemChanged.Invoke(this, EventArgs.Empty);
            }
        }

        private void SetVariationList(int index)
        {
            if (this.VariationType != VariationTypeEnum.NONE)
            {
                this.variationList = this.GetVariations(this.SelectedMediaItem);
   
                this.selectedVariationIndex = index < 0 || index >= this.variationList.Count ? this.variationList.Count - 1 : index;
                this.SelectedMediaItem.ChangeVariation(this.variationList[this.selectedVariationIndex]);
            }
            else
            {
                this.SelectedMediaItem.ResetVariation();
            }
        }

        private List<Variation> GetVariations(MediaItem mItem)
        {
            List<Variation> vList = MediaBrowserContext.GetVariations(mItem);

            if (this.VariationType == VariationTypeEnum.SAME_NAME)
            {
                vList = vList.Where(x => x.Name.Equals(
                    vList.FirstOrDefault(y => y.Id == mItem.VariationIdDefault).Name
                    , StringComparison.InvariantCultureIgnoreCase)).ToList();
            }

            return vList;
        }

        public void NewVariation(Variation variation)
        {
            this.variationList = this.GetVariations(this.SelectedMediaItem);
            this.selectedVariationIndex = this.variationList.FindLastIndex(x => x.Id == variation.Id);

            if (this.OnSelectedItemChanged != null)
            {
                this.OnSelectedItemChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public int CountUndeleted
        {
            get
            {
                return this.itemList.Count(x => !x.IsDeleted);
            }
        }

        public override string ToString()
        {
            return (this.SelectedMediaItemIndex + 1) + this.MinorVersion + "/" + this.itemList.Count;
        }
    }
}
