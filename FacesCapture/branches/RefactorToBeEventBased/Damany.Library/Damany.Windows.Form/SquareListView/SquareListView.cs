﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Damany.Windows.Form
{
    public delegate void CellDoubleClickHandler(object sender, CellDoubleClickEventArgs args);

    public partial class SquareListView : UserControl
    {
        public SquareListView()
        {
            InitializeComponent();

            this.DoubleBuffered = true;

            refreshTimer.Interval = 1000;
            refreshTimer.Enabled = false;
            refreshTimer.AutoReset = true;
            refreshTimer.Elapsed += refreshTimer_Elapsed;

            this.AutoDisposeImage = true;

            this.MaxCountOfCells = 25;
            this.cells = new List<Cell>(this.MaxCountOfCells);
            this.numOfColumns = 2;
            this.numOfRows = 2;


            this.PopulateCellList();
            this.CalcLayout();

            this.Resize += SquareListView_Resize;
            this.MouseDoubleClick += new MouseEventHandler(SquareListView_MouseDoubleClick);

            this.PaddingChanged += (sender, args) => this.CalcLayout();

            this.LastSelectedCell = Cell.Empty;
        }

        void SquareListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Cell c = this.CellFromPoint(e.Location);

            if (c == null)
            {
                return;
            }

            this.FireCellDoubleClick(c);

        }

        private void validateIndexRange(int idx)
        {
            if (idx > this.CellsCount - 1)
                throw new System.IndexOutOfRangeException();
        }

        public Image this[int idx]
        {
            get
            {
                validateIndexRange(idx);
                return this.cells[idx].Image;
            }

            set
            {
                validateIndexRange(idx);
                this.cells[idx].Image = value;
            }

        }




        private void FireSelectedCellChanged()
        {
            if (SelectedCellChanged != null)
            {
                SelectedCellChanged(this, EventArgs.Empty);
            }
        }

        private void FireCellDoubleClick(Cell c)
        {
            if (CellDoubleClick != null)
            {
                CellDoubleClickEventArgs arg = new CellDoubleClickEventArgs();
                arg.Cell = new ImageCell()
                {
                    Image = c.Image,
                    Path = c.Path,
                    Text = c.Text,
                    Tag = c.Tag,

                };

                CellDoubleClick(this, arg);
            }
        }




        void refreshTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (imgQueue.Count <= 0)
                {
                    this.refreshTimer.Enabled = false;
                    return;
                }

                RepositionCursor();

                Cell dstCell = this.cells[cursor];
                ImageCell imgToShow = this.imgQueue.Dequeue();

                if (this.AutoDisposeImage && dstCell.Image != null)
                {
                    dstCell.Image.Dispose();
                }

                dstCell.Image = imgToShow.Image;
                dstCell.Text = imgToShow.Text;
                dstCell.Path = imgToShow.Path;
                dstCell.Tag = imgToShow.Tag;

                this.Invalidate(Rectangle.Round(dstCell.Rec));
                cursor++;
            }
            catch (InvalidOperationException ex)// the queue is empty
            {
                this.refreshTimer.Enabled = false;
            }

            System.Diagnostics.Debug.WriteLine("tick");

        }

        public bool AutoDisposeImage { get; set; }


        public void ShowImages(ImageCell[] imgs)
        {
            Array.ForEach(imgs, imgQueue.Enqueue);

            if (imgQueue.Count > 0 && this.Visible)
            {
                refreshTimer.Enabled = true;
                System.Diagnostics.Debug.WriteLine("tick");
            }
        }



        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(this.BackColor);
            ControlPaint.DrawBorder(e.Graphics, this.ClientRectangle, Color.Gray, ButtonBorderStyle.Solid);
        }


        private void CalcLayout()
        {
            int width = this.ClientRectangle.Width / this.NumberOfColumns;
            int height = this.ClientRectangle.Height / this.NumberofRows;

            for (int i = 0; i < this.numOfRows; i++)
            {
                for (int j = 0; j < this.numOfColumns; j++)
                {
                    int idx = j + i * this.numOfColumns;
                    this.cells[idx].Rec = new Rectangle(j * width + this.Padding.Left,
                        i * height + this.Padding.Top,
                        width - this.Padding.Horizontal,
                        height - this.Padding.Vertical);

                    this.cells[idx].Column = j;
                    this.cells[idx].Row = i;
                    this.cells[idx].Index = idx;
                }
            }

            this.Invalidate();
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            for (int i = 0; i < this.CellsCount; i++)
            {
                Cell c = this.cells[i];
                if (e.ClipRectangle.IntersectsWith(Rectangle.Round(c.Rec)))
                {
                    c.Paint(e.Graphics, this.Font);
                }
            }

        }

        private Cell CellFromPoint(Point pt)
        {
            foreach (Cell c in this.cells)
            {
                if (c.Rec.Contains(pt))
                {
                    return c;
                }
            }

            return null;
        }

        private void PaintSelectedCell(Cell c)
        {
            LastSelectedCell.Selected = false;
            this.Invalidate(Rectangle.Round(LastSelectedCell.Rec));

            c.Selected = true;
            this.Invalidate(Rectangle.Round(c.Rec));
        }


        private void SquareListView_MouseClick(object sender, MouseEventArgs e)
        {
            Cell c = CellFromPoint(e.Location);
            if (c != null)
            {
                this.SelectedCell = c;
            }
        }

        void PopulateCellList()
        {
            int length = this.MaxCountOfCells;
            for (int i = 0; i < length; i++)
            {
                cells.Add(new Cell());
            }
        }

        void SquareListView_Resize(object sender, EventArgs e)
        {
            this.CalcLayout();
            this.Invalidate();
        }

        void RepositionCursor()
        {
            if (cursor > this.CellsCount - 1)
            {
                cursor = 0;
            }
        }


        public int NumberofRows
        {
            get
            {
                return numOfRows;
            }
            set
            {
                if (numOfRows == value)
                    return;

                if (value * numOfColumns > this.MaxCountOfCells)
                {
                    throw new ArgumentOutOfRangeException(@"NumberOfRows",
                        @"Total Number of Cells > Max Number of cells");
                }

                numOfRows = value;

                if (this.SelectedCell != null)
                {
                    this.SelectedCell.Selected = false;
                }

                this.CalcLayout();
                this.Invalidate();
            }
        }


        public int NumberOfColumns
        {
            get
            {
                return numOfColumns;
            }
            set
            {
                if (numOfColumns == value)
                    return;

                if (value * numOfRows > this.MaxCountOfCells)
                {
                    throw new ArgumentOutOfRangeException(@"NumberOfColumns",
                        @"Total Number of Cells > Max Number of cells");
                }

                numOfColumns = value;

                if (this.SelectedCell != null)
                {
                    this.SelectedCell.Selected = false;
                }

                this.CalcLayout();
                this.Invalidate();
            }
        }


        public Cell HitTest(Point pt)
        {
            return CellFromPoint(pt);
        }

        public Cell HitTest(int x, int y)
        {
            return CellFromPoint(new Point(x, y));
        }




        private Cell _SelectedCell;
        public Cell SelectedCell
        {
            get
            {
                return _SelectedCell;
            }
            set
            {
                if (_SelectedCell == value)
                    return;

                _SelectedCell = value;

                this.PaintSelectedCell(value);

                FireSelectedCellChanged();

                LastSelectedCell = value;
            }
        }


        public Cell LastSelectedCell { get; private set; }


        public int CellsCount { get { return this.numOfColumns * this.numOfRows; } }
        public int MaxCountOfCells { get; set; }


        public event EventHandler SelectedCellChanged;
        public event CellDoubleClickHandler CellDoubleClick;


        int cursor = 0;
        IList<Cell> cells;
        System.Timers.Timer refreshTimer = new System.Timers.Timer();
        Queue<ImageCell> imgQueue = new Queue<ImageCell>();
        private int numOfColumns;
        private int numOfRows;
    }
}
