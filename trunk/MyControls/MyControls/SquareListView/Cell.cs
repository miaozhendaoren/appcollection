﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MyControls
{
    public class Cell
    {
        public RectangleF Rec { get; set; }
        public Image Image { get; set; }
        public bool Selected { get; set; }

        private string _ImagePath;
        public string Path
        {
            get
            {
                return _ImagePath;
            }
            set
            {
                if (_ImagePath == value)
                    return;
                _ImagePath = value;
            }
        }


        public string Text { get; set; }

        private void DrawStringInCenterOfRectangle(string str, Graphics g, Font font, RectangleF rec)
        {
            SizeF sizeOfPrompt = g.MeasureString(str, font);
            float x = rec.X + (rec.Width - sizeOfPrompt.Width) / 2;
            float y = rec.Y + (rec.Height - sizeOfPrompt.Height) / 2;
            Brush br = this.Selected ? Brushes.White : SystemBrushes.ControlText;
            g.DrawString(str, font, br, x, y);
        }

        public Rectangle ToRectangle(RectangleF rF)
        {
            return new Rectangle((int)rF.X, (int)rF.Y, (int)rF.Width, (int)rF.Height);
        }


        public void CenterRectangleRelativeTo(Rectangle destRectangle, ref Rectangle srcRectangle)
        {
            int x = destRectangle.X + (destRectangle.Width - srcRectangle.Width) / 2;
            int y = destRectangle.Y + (destRectangle.Height - srcRectangle.Height) / 2;

            srcRectangle.Offset(x, y);
        }

        public Rectangle CalculateAutoFitRectangle(Rectangle destRectangle, Rectangle srcRectangle)
        {
            Rectangle resultRec = srcRectangle;

            //scale the rectangle
            if (srcRectangle.Width > destRectangle.Width 
                || srcRectangle.Height > destRectangle.Height)
            {
                float xScale = (float)destRectangle.Width / srcRectangle.Width;
                float yScale = (float)destRectangle.Height / srcRectangle.Height;

                float scale = Math.Min(xScale, yScale);
                resultRec.Height = (int)(resultRec.Height * scale);
                resultRec.Width = (int)(resultRec.Width * scale);
            }

            CenterRectangleRelativeTo(destRectangle, ref resultRec);
            return resultRec;
        }

        public void Paint(Graphics g, Font font)
        {
            if (this.Selected)
            {
                g.FillRectangle(Brushes.DarkBlue, this.Rec);
            }

            if (this.Image == null)
            {
                DrawStringInCenterOfRectangle("未指定图片", g, font, this.Rec);
                g.DrawRectangle(Pens.Gray, ToRectangle(this.Rec));
            }
            else
            {
                SizeF sizeOfText = SizeF.Empty;
                int space = 3;

                if (!string.IsNullOrEmpty(this.Text))
	            {
                    sizeOfText = g.MeasureString(this.Text, font);
                    RectangleF recText = this.Rec;
                    float h = this.Rec.Height - sizeOfText.Height - space;
                    recText.Offset(0, h);
                    recText.Height -= h;
                    DrawStringInCenterOfRectangle(this.Text, g, font, recText);
	            }

                RectangleF recOfImg = this.Rec;
                recOfImg.Height -= sizeOfText.Height + space;

                g.DrawImage(this.Image, 
                    CalculateAutoFitRectangle(ToRectangle(recOfImg), 
                    new Rectangle(0, 0, this.Image.Width, this.Image.Height)));

                g.DrawRectangle(Pens.Gray, ToRectangle(recOfImg));
            }
        }
    }
}
