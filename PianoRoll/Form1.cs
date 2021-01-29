using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using SMF;

namespace PianoRoll {
    public partial class Form1 : Form {
        private struct DrawMeasure {
            public bool IsBar;
            public int Tick;
            public int Number;
        }

        private struct DrawNote {
            public int track;
            public int begin;
            public int end;
            public int data1;
            public int data2;
            public DrawNote(int track, int begin, params byte[] data) {
                this.track = track;
                this.begin = begin;
                end = -1;
                data1 = 2 <= data.Length ? data[1] : 0;
                data2 = 3 <= data.Length ? data[2] : 0;
            }
        }

        private List<DrawMeasure> mDrawMeasureList = new List<DrawMeasure>();
        private List<DrawNote> mDrawNoteList = new List<DrawNote>();

        private static readonly Dictionary<string, int> Snaps = new Dictionary<string, int> {
            { "4分",    960 },
            { "3連4分", 640 },
            { "8分",    480 },
            { "5連4分", 384 },
            { "3連8分", 320 },
            { "16分",   240 },
            { "5連8分", 192 },
            { "3連16分",160 },
            { "32分",   120 },
            { "5連16分", 96 },
            { "3連32分", 80 },
            { "64分",    60 },
            { "5連32分", 48 },
            { "3連64分", 40 },
            { "5連64分", 24 }
        };

        private static readonly int[] TimeScales = new int[] {
            120, 240, 480, 960, 1920
        };

        private static readonly int[] ToneHeights = new int[] {
            28, 24, 20, 18, 15, 12, 10, 8, 6, 5
        };

        private static readonly StringFormat NoteNameFormat = new StringFormat() {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near
        };

        private static readonly Pen Gray12 = new Pen(Color.FromArgb(167, 31, 31, 31), 1.0f);
        private static readonly Pen Gray75 = new Pen(Color.FromArgb(167, 191, 191, 191), 1.0f);
        private static readonly Pen Gray80 = new Pen(Color.FromArgb(167, 204, 204, 204), 1.0f);
        private static readonly Pen Gray90 = new Pen(Color.FromArgb(167, 229, 229, 229), 1.0f);

        private static readonly Pen FocusToneColor = new Pen(Color.FromArgb(95, 95, 255, 95), 1.0f);

        private static readonly Brush SolidToneColor = new Pen(Color.FromArgb(255, 10, 224, 10), 1.0f).Brush;
        private static readonly Pen SolidToneColorH = new Pen(Color.FromArgb(255, 105, 245, 105), 1.0f);
        private static readonly Pen SolidToneColorL = new Pen(Color.FromArgb(255, 15, 177, 15), 1.0f);

        private static readonly Brush BackToneColor = new Pen(Color.FromArgb(255, 10, 214, 214), 1.0f).Brush;
        private static readonly Pen BackToneColorH = new Pen(Color.FromArgb(255, 95, 245, 245), 1.0f);
        private static readonly Pen BackToneColorL = new Pen(Color.FromArgb(255, 15, 167, 167), 1.0f);

        private static readonly Brush SelectAreaColor = new Pen(Color.FromArgb(32, 0, 255, 0), 1.0f).Brush;
        private static readonly Pen SelectBorderColor = new Pen(Color.DarkBlue) {
            Width = 1.0f,
            DashStyle = DashStyle.Dot
        };

        private Bitmap mBmpRoll;
        private Graphics mgRoll;

        private Font mNoteNameFont = new Font("MS Gothic", 9.0f, FontStyle.Regular);
        private Font mMeasureFont = new Font("MS Gothic", 11.0f, FontStyle.Regular);

        private int mTimeSnap = 240;
        private int mQuarterNoteWidth = 96;

        private int mTimeScaleIdx = 3;
        private int mTimeScale = TimeScales[3];
        private int mToneHeightIdx = 6;
        private int mToneHeight = ToneHeights[6];

        private Point mCursor;
        private int mDispTones;
        private int mToneBegin;
        private int mToneEnd;
        private int mTimeBegin;
        private int mTimeEnd;

        private bool mIsDrag = false;
        private bool mIsSelect = false;
        private bool mPressAlt = false;
        private Keys mPressKey = Keys.None;

        private List<Event> mEventList = new List<Event>();

        public List<int> DispTrack = new List<int>();
        public int EditTrack = 2;

        public Form1() {
            InitializeComponent();
            setSize();
            setInputMode(tsbRoll);
            setSelectWrite(tsbWrite);
            setEditMode(tsmEditModeNote);
            setTimeDiv(tsmTick240);

            picRoll.MouseWheel += new MouseEventHandler(picRoll_MouseWheel);
            KeyDown += new KeyEventHandler(picRoll_KeyDown);
            KeyUp += new KeyEventHandler(picRoll_KeyUp);

            DispTrack.Add(1);
            DispTrack.Add(2);
            DispTrack.Add(3);
            DispTrack.Add(4);
            DispTrack.Add(5);
            DispTrack.Add(6);
            DispTrack.Add(7);
            DispTrack.Add(8);
            hScroll.Minimum = 0;
            hScroll.Maximum = 960 * 4 * 16;
            var s = new SMF.SMF("C:\\Users\\9004054911\\Desktop\\Media\\town.mid");
            foreach (var e in s.EventList) {
                mEventList.Add(e);
            }
            hScroll.Maximum = s.MaxTime;

            timer1.Interval = 16;
            timer1.Enabled = true;
            timer1.Start();
            vScroll.Value = vScroll.Minimum < 80 ? 80 : vScroll.Minimum;
        }

        private void Form1_SizeChanged(object sender, EventArgs e) {
            setSize();
        }

        private void timer1_Tick(object sender, EventArgs e) {
            drawRoll();
        }

        private void tsbRoll_Click(object sender, EventArgs e) {
            setInputMode(sender);
        }

        private void tsbEventList_Click(object sender, EventArgs e) {
            setInputMode(sender);
        }

        private void setInputMode(object item) {
            tsbRoll.Checked = false;
            tsbRoll.Image = Properties.Resources.pianoroll_disable;
            tsbEventList.Checked = false;
            tsbEventList.Image = Properties.Resources.eventlist_disable;

            var obj = (ToolStripButton)item;
            obj.Checked = true;

            switch (obj.Name) {
            case "tsbRoll":
                tsbRoll.Image = Properties.Resources.pianoroll;
                break;
            case "tsbEventList":
                tsbEventList.Image = Properties.Resources.eventlist;
                break;
            }
        }

        private void tsbSelect_Click(object sender, EventArgs e) {
            setSelectWrite(tsbSelect);
        }

        private void tsbWrite_Click(object sender, EventArgs e) {
            setSelectWrite(tsbWrite);
        }

        private void setSelectWrite(object item) {
            tsbWrite.Checked = false;
            tsbWrite.Image = Properties.Resources.write_disable;
            tsbSelect.Checked = false;
            tsbSelect.Image = Properties.Resources.select_disable;

            var obj = (ToolStripButton)item;
            obj.Checked = true;

            switch (obj.Name) {
            case "tsbWrite":
                tsbWrite.Image = Properties.Resources.write;
                break;
            case "tsbSelect":
                tsbSelect.Image = Properties.Resources.select;
                break;
            }
        }

        #region tsmTick event
        private void tsmTick960_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick480_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick240_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick120_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick060_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick640_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick320_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick160_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick080_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick040_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick384_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick192_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick096_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick048_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void tsmTick024_Click(object sender, EventArgs e) {
            setTimeDiv(sender);
        }

        private void setTimeDiv(object item) {
            tsmTick960.Checked = false;
            tsmTick480.Checked = false;
            tsmTick240.Checked = false;
            tsmTick120.Checked = false;
            tsmTick060.Checked = false;

            tsmTick640.Checked = false;
            tsmTick320.Checked = false;
            tsmTick160.Checked = false;
            tsmTick080.Checked = false;
            tsmTick040.Checked = false;

            tsmTick384.Checked = false;
            tsmTick192.Checked = false;
            tsmTick096.Checked = false;
            tsmTick048.Checked = false;
            tsmTick024.Checked = false;

            var obj = (ToolStripMenuItem)item;
            obj.Checked = true;
            tsdTimeDiv.Image = obj.Image;
            tsdTimeDiv.ToolTipText = string.Format("入力単位({0})", obj.Text);

            mTimeSnap = Snaps[obj.Text];
            if (mTimeSnap % 15 == 0) {
                mQuarterNoteWidth = 96;
            } else if (mTimeSnap % 5 == 0) {
                mQuarterNoteWidth = 96;
            } else if (mTimeSnap % 3 == 0) {
                mQuarterNoteWidth = 120;
            }

            hScroll.LargeChange = mTimeSnap;
            hScroll.SmallChange = mTimeSnap;

            drawRoll();
        }
        #endregion

        #region tsmEditMode event
        private void tsmEditModeNote_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeInst_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeVol_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeExp_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModePan_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModePitch_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeVibDep_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeVibRate_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeRev_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeDelDep_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeDelTime_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeCho_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeFc_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeFq_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeAttack_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeRelease_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void tsmEditModeTempo_Click(object sender, EventArgs e) {
            setEditMode(sender);
        }

        private void setEditMode(object item) {
            tsmEditModeNote.Checked = false;
            tsmEditModeInst.Checked = false;
            tsmEditModeVol.Checked = false;
            tsmEditModeExp.Checked = false;
            tsmEditModePan.Checked = false;
            tsmEditModePitch.Checked = false;
            tsmEditModeVib.Checked = false;
            tsmEditModeVibDep.Checked = false;
            tsmEditModeVibRate.Checked = false;
            tsmEditModeRev.Checked = false;
            tsmEditModeDel.Checked = false;
            tsmEditModeDelDep.Checked = false;
            tsmEditModeDelTime.Checked = false;
            tsmEditModeCho.Checked = false;
            tsmEditModeFc.Checked = false;
            tsmEditModeFq.Checked = false;
            tsmEditModeAttack.Checked = false;
            tsmEditModeRelease.Checked = false;
            tsmEditModeTempo.Checked = false;

            var obj = (ToolStripMenuItem)item;
            obj.Checked = true;
            tsdEditMode.Image = obj.Image;
            tsdEditMode.ToolTipText = string.Format("入力種別({0})", obj.Text);

            tsmEditModeVib.Checked = tsmEditModeVibDep.Checked | tsmEditModeVibRate.Checked;
            tsmEditModeDel.Checked = tsmEditModeDelDep.Checked | tsmEditModeDelTime.Checked;
        }
        #endregion

        #region picRoll event
        private void picRoll_MouseDown(object sender, MouseEventArgs e) {
            switch (e.Button) {
            case MouseButtons.Right:
                break;
            case MouseButtons.Left:
                mTimeBegin = snapTimeScroll(mCursor.X);
                mToneBegin = 127 + vScroll.Minimum - vScroll.Value - mCursor.Y / mToneHeight;
                mIsDrag = true;
                break;
            }
        }

        private void picRoll_MouseUp(object sender, MouseEventArgs e) {
            switch (e.Button) {
            case MouseButtons.Right:
                break;
            case MouseButtons.Left:
                if (mTimeEnd < mTimeBegin) {
                    var tmp = mTimeEnd;
                    mTimeEnd = mTimeBegin;
                    if (tsbWrite.Checked) {
                        mTimeEnd += mTimeSnap;
                    }
                    mTimeBegin = tmp;
                } else {
                    mTimeEnd += mTimeSnap;
                }
                if (mToneEnd < mToneBegin) {
                    var tmp = mToneEnd;
                    mToneEnd = mToneBegin;
                    mToneBegin = tmp;
                }
                if (tsbWrite.Checked && tsmEditModeNote.Checked) {
                    addNoteEvent();
                    putDrawEvents();
                }
                if (tsbSelect.Checked && tsmEditModeNote.Checked) {
                    mIsSelect = true;
                }
                mIsDrag = false;
                break;
            }
        }

        private void picRoll_MouseMove(object sender, MouseEventArgs e) {
            snapCursor(picRoll.PointToClient(Cursor.Position));
            mTimeEnd = snapTimeScroll(mCursor.X);
            mToneEnd = 127 + vScroll.Minimum - vScroll.Value - mCursor.Y / mToneHeight;
            if (mToneEnd < 0) {
                mToneEnd = 0;
            }
            if (tsbWrite.Checked) {
                mToneBegin = mToneEnd;
            }
        }

        private void picRoll_MouseWheel(object sender, MouseEventArgs e) {
            switch (mPressKey) {
            case Keys.None:
                if (0 < Math.Sign(e.Delta)) {
                    if (vScroll.Minimum <= vScroll.Value - 1) {
                        vScroll.Value--;
                    }
                } else {
                    if (vScroll.Value < vScroll.Maximum) {
                        vScroll.Value++;
                    }
                }
                break;
            case Keys.ControlKey:
                if (0 < Math.Sign(e.Delta)) {
                    timeZoom();
                } else {
                    timeZoomout();
                }
                putDrawEvents();
                break;
            case Keys.ShiftKey:
                if (0 < Math.Sign(e.Delta)) {
                    toneZoom();
                } else {
                    toneZoomout();
                }
                break;
            default:
                break;
            }
        }

        private void picRoll_KeyDown(object sender, KeyEventArgs e) {
            mPressKey = e.KeyCode;
            mPressAlt = e.Alt;
        }

        private void picRoll_KeyUp(object sender, KeyEventArgs e) {
            mPressKey = Keys.None;
            mPressAlt = false;

            switch(e.KeyCode) {
            case Keys.F1:
                setSelectWrite(tsbWrite);
                break;
            case Keys.F2:
                setSelectWrite(tsbSelect);
                break;
            case Keys.F3:
                setEditMode(tsmEditModeNote);
                break;
            }
        }
        #endregion

        #region zoom event
        private void tsbTimeZoom_Click(object sender, EventArgs e) {
            timeZoom();
            putDrawEvents();
        }

        private void tsbTimeZoomout_Click(object sender, EventArgs e) {
            timeZoomout();
            putDrawEvents();
        }

        private void tsbToneZoom_Click(object sender, EventArgs e) {
            toneZoom();
        }

        private void tsbToneZoomout_Click(object sender, EventArgs e) {
            toneZoomout();
        }

        private void timeZoom() {
            if (0 < mTimeScaleIdx) {
                mTimeScaleIdx--;
                mTimeScale = TimeScales[mTimeScaleIdx];
            }
            if (0 == mTimeScaleIdx) {
                tsbTimeZoom.Enabled = false;
            }
            tsbTimeZoomout.Enabled = true;
        }

        private void timeZoomout() {
            if (mTimeScaleIdx < TimeScales.Length - 1) {
                mTimeScaleIdx++;
                mTimeScale = TimeScales[mTimeScaleIdx];
            }
            if (mTimeScaleIdx == TimeScales.Length - 1) {
                tsbTimeZoomout.Enabled = false;
            }
            tsbTimeZoom.Enabled = true;
        }

        private void toneZoom() {
            if (0 < mToneHeightIdx) {
                mToneHeightIdx--;
                mToneHeight = ToneHeights[mToneHeightIdx];
                setSize();
            }
            if (0 == mToneHeightIdx) {
                tsbToneZoom.Enabled = false;
            }
            tsbToneZoomout.Enabled = true;
            snapCursor(mCursor);
        }

        private void toneZoomout() {
            if (mToneHeightIdx < ToneHeights.Length - 1) {
                mToneHeightIdx++;
                mToneHeight = ToneHeights[mToneHeightIdx];
                setSize();
            }
            if (mToneHeightIdx == ToneHeights.Length - 1) {
                tsbToneZoomout.Enabled = false;
            }
            tsbToneZoom.Enabled = true;
            snapCursor(mCursor);
        }
        #endregion

        private int snapTimeScroll(int x = 0) {
            return hScroll.Value / mTimeSnap * mTimeSnap + mTimeScale * x / mQuarterNoteWidth;
        }

        private void snapCursor(Point pos) {
            if (picRoll.Width <= pos.X) {
                pos.X = picRoll.Width - 1;
                if (mIsDrag && hScroll.Value + hScroll.SmallChange < hScroll.Maximum) {
                    hScroll.Value += hScroll.SmallChange;
                }
            }
            if (pos.X < 0) {
                pos.X = 0;
                if (mIsDrag && 0 <= hScroll.Value - hScroll.SmallChange) {
                    hScroll.Value -= hScroll.SmallChange;
                }
            }
            var divX = mQuarterNoteWidth * mTimeSnap / mTimeScale;
            pos.X = pos.X / divX * divX;


            if (picRoll.Height <= pos.Y) {
                pos.Y = picRoll.Height - 1;
                if (tsbSelect.Checked && mIsDrag && vScroll.Value < vScroll.Maximum) {
                    vScroll.Value++;
                }
            }
            if (pos.Y < 0) {
                pos.Y = 0;
                if (tsbSelect.Checked && mIsDrag && vScroll.Minimum <= vScroll.Value - 1) {
                    vScroll.Value--;
                }
            }
            var ofsY = mBmpRoll.Height % mToneHeight;
            pos.Y = (pos.Y - ofsY) / mToneHeight * mToneHeight;

            var tone = pos.Y / mToneHeight;
            if (tone < 0) {
                tone = 0;
            } else if (127 < tone) {
                tone = 127;
            }

            mCursor.X = pos.X;
            mCursor.Y = tone * mToneHeight + ofsY;
        }

        private void setSize() {
            if (Height < toolStrip1.Bottom + hScroll.Height + 40) {
                Height = toolStrip1.Bottom + hScroll.Height + 40;
            }

            pnlRoll.Left = 5;
            pnlRoll.Top = toolStrip1.Bottom;
            pnlRoll.Width = Width - 21;
            pnlRoll.Height = Height - toolStrip1.Bottom - 39;

            vScroll.Top = 0;
            vScroll.Left = pnlRoll.Width - vScroll.Width;
            vScroll.Height = pnlRoll.Height - hScroll.Height;
            hScroll.Left = 0;
            hScroll.Top = pnlRoll.Height - hScroll.Height;
            hScroll.Width = pnlRoll.Width - vScroll.Width;

            picRoll.Left = 0;
            picRoll.Top = 0;
            picRoll.Width = vScroll.Left;
            picRoll.Height = hScroll.Top;
            if (picRoll.Width == 0) {
                picRoll.Width = 1;
            }
            if (picRoll.Height == 0) {
                picRoll.Height = 1;
            }

            mBmpRoll = new Bitmap(picRoll.Width, picRoll.Height);
            mgRoll = Graphics.FromImage(mBmpRoll);

            mDispTones = picRoll.Height / mToneHeight;
            if (128 < mDispTones) {
                mDispTones = 128;
            }
            vScroll.Maximum = 128;
            vScroll.Minimum = mDispTones;
            vScroll.SmallChange = 1;
            vScroll.LargeChange = 1;
            hScroll.LargeChange = mTimeSnap;
            hScroll.SmallChange = mTimeSnap;

            int fontSize = 9;
            while (fontSize < 17) {
                var newFont = new Font(mNoteNameFont.Name, fontSize);
                var fsize = mgRoll.MeasureString("-1", newFont).Height - 3;
                if (mToneHeight <= fsize) {
                    break;
                }
                mNoteNameFont = newFont;
                fontSize++;
            }

            putDrawEvents();
            drawRoll();
        }

        private void drawRoll() {
            mgRoll.Clear(Color.FromArgb(255, 255, 245));

            var ofsY = mBmpRoll.Height % mToneHeight;

            // Roll
            for (int y = mDispTones - 1, no = 128 - vScroll.Value; -1 <= y; y--, no++) {
                var py = mToneHeight * y + ofsY;
                switch (no % 12) {
                case 1:
                case 3:
                case 6:
                case 8:
                case 10:
                    mgRoll.FillRectangle(Gray80.Brush, 0, py + 1, mBmpRoll.Width, mToneHeight - 1);
                    mgRoll.DrawLine(Gray75, 0, py, mBmpRoll.Width, py);
                    break;
                case 11:
                    mgRoll.DrawLine(Gray12, 0, py, mBmpRoll.Width, py);
                    break;
                default:
                    mgRoll.DrawLine(Gray75, 0, py, mBmpRoll.Width, py);
                    break;
                }
            }

            putDrawEvents();

            // Measure
            foreach (var ev in mDrawMeasureList) {
                var x = (ev.Tick - snapTimeScroll()) * mQuarterNoteWidth / mTimeScale;
                if (ev.IsBar) {
                    mgRoll.DrawLine(Pens.Black, x, 0, x, mBmpRoll.Height);
                } else {
                    mgRoll.DrawLine(Gray75, x, 0, x, mBmpRoll.Height);
                }
            }

            // Note
            foreach (var ev in mDrawNoteList) {
                if (ev.track == EditTrack) {
                    continue;
                }
                var tone = 127 + vScroll.Minimum - vScroll.Value - ev.data1;
                var x1 = (ev.begin - snapTimeScroll()) * mQuarterNoteWidth / mTimeScale;
                var x2 = (ev.end - snapTimeScroll()) * mQuarterNoteWidth / mTimeScale;
                var y1 = mToneHeight * tone + ofsY;
                var y2 = mToneHeight * (tone + 1) + ofsY;
                drawBackNote(x1, y1, x2, y2);
            }
            foreach (var ev in mDrawNoteList) {
                if (ev.track != EditTrack) {
                    continue;
                }
                var tone = 127 + vScroll.Minimum - vScroll.Value - ev.data1;
                var x1 = (ev.begin - snapTimeScroll()) * mQuarterNoteWidth / mTimeScale;
                var x2 = (ev.end - snapTimeScroll()) * mQuarterNoteWidth / mTimeScale;
                var y1 = mToneHeight * tone + ofsY;
                var y2 = mToneHeight * (tone + 1) + ofsY;
                drawNote(x1, y1, x2, y2);
            }

            // EditingNote
            if (mIsDrag) {
                var x1 = (mTimeBegin - snapTimeScroll()) * mQuarterNoteWidth / mTimeScale;
                var x2 = (mTimeEnd - snapTimeScroll()) * mQuarterNoteWidth / mTimeScale;
                if (x1 <= x2) {
                    x2 += mQuarterNoteWidth * mTimeSnap / mTimeScale;
                } else {
                    if (tsbWrite.Checked) {
                        x1 += mQuarterNoteWidth * mTimeSnap / mTimeScale;
                    }
                }
                if (x2 < x1) {
                    var tmp = x2;
                    x2 = x1;
                    x1 = tmp;
                }

                var y1 = (127 + vScroll.Minimum - vScroll.Value - mToneBegin) * mToneHeight + ofsY;
                var y2 = (127 + vScroll.Minimum - vScroll.Value - mToneEnd) * mToneHeight + ofsY;
                if (y1 <= y2) {
                    y2 += mToneHeight;
                } else {
                    y1 += mToneHeight;
                }
                if (y2 < y1) {
                    var tmp = y2;
                    y2 = y1;
                    y1 = tmp;
                }

                if (tsbSelect.Checked) {
                    mgRoll.FillRectangle(SelectAreaColor, x1, y1, x2 - x1, y2 - y1);
                    mgRoll.DrawRectangle(SelectBorderColor, x1, y1, x2 - x1, y2 - y1);
                }
                if (tsbWrite.Checked) {
                    drawNote(x1, y1, x2, y2);
                }
            } else {
                mgRoll.FillRectangle(FocusToneColor.Brush, 1, mCursor.Y + 1, mBmpRoll.Width - 1, mToneHeight);
                mgRoll.DrawLine(Pens.Red, mCursor.X, 0, mCursor.X, mBmpRoll.Height);
            }

            // Text
            for (int y = mDispTones - 1, no = 128 - vScroll.Value; 0 <= y; y--, no++) {
                if (no % 12 == 0) {
                    var py = mToneHeight * y + ofsY;
                    var name = (no / 12 - 1).ToString();
                    var fsize = mgRoll.MeasureString(name, mNoteNameFont).Height - 3;
                    mgRoll.DrawString(name, mNoteNameFont, Gray12.Brush, 0, py + mToneHeight - fsize);
                }
            }
            foreach (var ev in mDrawMeasureList) {
                if (ev.IsBar) {
                    var x = (ev.Tick - snapTimeScroll()) * mQuarterNoteWidth / mTimeScale;
                    mgRoll.DrawString(ev.Number.ToString(), mMeasureFont, Brushes.Black, x, 0);
                }
            }

            picRoll.Image = mBmpRoll;
        }

        private void drawNote(int x1, int y1, int x2, int y2) {
            x1 += 1;
            y1 += 1;
            mgRoll.FillRectangle(SolidToneColor, x1, y1, x2 - x1 + 1, y2 - y1 + 1);
            mgRoll.DrawLine(SolidToneColorL, x1, y2, x2, y2);
            mgRoll.DrawLine(SolidToneColorH, x1, y1, x2 - 1, y1);
            mgRoll.DrawLine(SolidToneColorL, x2, y2, x2, y1 + 1);
            mgRoll.DrawLine(SolidToneColorH, x1, y1, x1, y2 - 1);
        }

        private void drawBackNote(int x1, int y1, int x2, int y2) {
            x1 += 1;
            y1 += 1;
            mgRoll.FillRectangle(BackToneColor, x1, y1, x2 - x1 + 1, y2 - y1 + 1);
            mgRoll.DrawLine(BackToneColorL, x1, y2, x2, y2);
            mgRoll.DrawLine(BackToneColorH, x1, y1, x2 - 1, y1);
            mgRoll.DrawLine(BackToneColorL, x2, y2, x2, y1 + 1);
            mgRoll.DrawLine(BackToneColorH, x1, y1, x1, y2 - 1);
        }

        private void addNoteEvent() {
            var noteOnList = new List<DrawNote>();
            foreach (var ev in mEventList) {
                if (ev.Track != EditTrack) {
                    continue;
                }
                switch (ev.Type) {
                case E_STATUS.NOTE_OFF:
                    for (int i = 0; i < noteOnList.Count; i++) {
                        var dispEv = noteOnList[i];
                        if (dispEv.data1 == ev.Data[1]) {
                            if (dispEv.end == -1) {
                                dispEv.end = ev.Tick;
                                if (mTimeEnd <= dispEv.begin || dispEv.end <= mTimeBegin) {
                                    noteOnList.RemoveAt(i);
                                    i--;
                                }
                            }
                        }
                    }
                    break;
                case E_STATUS.NOTE_ON:
                    if (mToneBegin == ev.Data[1] && ev.Tick <= mTimeEnd) {
                        noteOnList.Add(new DrawNote(ev.Track, ev.Tick, ev.Data));
                    }
                    break;
                }
            }

            if (noteOnList.Count == 0) {
                mEventList.Add(new Event(mTimeBegin, EditTrack, 0, E_STATUS.NOTE_ON, mToneBegin, 127));
                mEventList.Add(new Event(mTimeEnd, EditTrack, 0, E_STATUS.NOTE_OFF, mToneBegin, 0));
            } else {
                var neighbor = noteOnList[0];
                foreach(var n in noteOnList) {
                    if (n.begin < neighbor.begin) {
                        neighbor = n;
                    }
                }
                if (mTimeBegin < neighbor.begin) {
                    mEventList.Add(new Event(mTimeBegin, EditTrack, 0, E_STATUS.NOTE_ON, mToneBegin, 127));
                    mEventList.Add(new Event(neighbor.begin, EditTrack, 0, E_STATUS.NOTE_OFF, mToneBegin, 0));
                }
            }
            mEventList.Sort(Event.Compare);
        }

        private void putDrawEvents() {
            mDrawNoteList.Clear();
            var beginTime = snapTimeScroll();
            var endTime = snapTimeScroll(mBmpRoll.Width);
            var dispNoteList = new List<DrawNote>();
            foreach (var ev in mEventList) {
                switch (ev.Type) {
                case E_STATUS.NOTE_OFF:
                    for (int i = 0; i < dispNoteList.Count; i++) {
                        var dispEv = dispNoteList[i];
                        if (dispEv.track == ev.Track && dispEv.data1 == ev.Data[1]) {
                            if (beginTime <= ev.Tick) {
                                dispEv.end = ev.Tick;
                                mDrawNoteList.Add(dispEv);
                            }
                            dispNoteList.RemoveAt(i);
                            i--;
                        }
                    }
                    break;
                case E_STATUS.NOTE_ON:
                    if (ev.Tick <= endTime) {
                        dispNoteList.Add(new DrawNote(ev.Track, ev.Tick, ev.Data));
                    }
                    break;
                }
            }

            var mesureDeno = 4;
            var mesureNume = 4;
            var mesureTick = 0;
            var mesureNum = 1;
            var mesureInterval = 3840;
            var beatInterval = 960;
            mDrawMeasureList.Clear();
            foreach (var ev in mEventList) {
                if (ev.Type != E_STATUS.META || ev.Meta.Type != E_META.MEASURE) {
                    continue;
                }
                for (; mesureTick < ev.Tick; mesureTick += mesureInterval) {
                    for (int beatTick = 0; beatTick < mesureInterval; beatTick += beatInterval) {
                        var tick = mesureTick + beatTick;
                        if (beginTime <= tick && tick <= endTime) {
                            var mesure = new DrawMeasure();
                            mesure.IsBar = beatTick == 0;
                            mesure.Tick = tick;
                            mesure.Number = mesureNum;
                            mDrawMeasureList.Add(mesure);
                        }
                    }
                    mesureNum++;
                }
                var m = new Mesure(ev.Meta.Int);
                mesureDeno = m.denominator;
                mesureNume = m.numerator;
                mesureInterval = 3840 * mesureNume / mesureDeno;
                beatInterval = 3840 / mesureDeno;
            }

            for (; mesureTick <= endTime; mesureTick += mesureInterval) {
                for (int beatTick = 0; beatTick < mesureInterval; beatTick += beatInterval) {
                    var tick = mesureTick + beatTick;
                    if (beginTime <= tick && tick <= endTime) {
                        var mesure = new DrawMeasure();
                        mesure.IsBar = beatTick == 0;
                        mesure.Tick = tick;
                        mesure.Number = mesureNum;
                        mDrawMeasureList.Add(mesure);
                    }
                }
                mesureNum++;
            }
        }
    }
}
