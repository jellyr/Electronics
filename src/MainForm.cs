﻿using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Electronics
{
    public partial class MainForm : Form
    {
        readonly int defaultCellSize = 64;
        readonly string resourcePath = "Misc/Resources";

        int textLabelSize = 20;
        int scale = 1;
        bool drawLines = true;
        bool mousePressed = false;
        bool updateEnabled = true;
        bool gridLoaded = false;
        bool formLoaded = false;
        int loadX, loadY;
        bool fileSaved = false;
        string lastFileDirectory = null;

        SerializedGrid gridToAdd;
        Grid grid;
        GridDrawer drawer;
        Type drawObjectType = typeof(Wire);
        Color BackgroundColor;


        public MainForm()
        {
            InitializeComponent();
        }

        private void OnAppLoad(object sender, EventArgs e)
        {
            DoubleBuffered = true;
            BackgroundColor = Color.FromArgb(33, 33, 33);
            mainGrid.BackColor = BackgroundColor;

            int numOfRows = 64;
            int numOfColumns = 64;
            grid = new Grid(numOfColumns, numOfRows);

            drawer = new GridDrawer(grid);
            try
            {
                drawer.AddFolder(resourcePath, defaultCellSize, scale);
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                MessageBox.Show("директория не найдена: " + resourcePath, "Произошла ошибка при открытии Electronics");
                Close();
            }

            var fileItem = new ToolStripMenuItem("File");

            var newFileItem = new ToolStripMenuItem("New file")
            {
                ShortcutKeys = Keys.Control | Keys.F
            };
            newFileItem.Click += (object o, EventArgs args) =>
            {
                SaveFile(o, args);
                lastFileDirectory = null;
                grid.Clear();
            };
            fileItem.DropDownItems.Add(newFileItem);


            var saveAsItem = new ToolStripMenuItem("SaveAs")
            {
                ShortcutKeys = Keys.Control | Keys.T
            };
            saveAsItem.Click += SaveAsFile;
            fileItem.DropDownItems.Add(saveAsItem);

            var saveItem = new ToolStripMenuItem("Save")
            {
                ShortcutKeys = Keys.Control | Keys.S
            };
            saveItem.Click += SaveFile;
            fileItem.DropDownItems.Add(saveItem);

            var loadItem = new ToolStripMenuItem("Load");
            loadItem.Click += (object o, EventArgs args) =>
            {
                Stream fileStream;
                OpenFileDialog loadFileDialog = new OpenFileDialog()
                {
                    Filter = "(*.elc)|*.elc",
                    FilterIndex = 2,
                    RestoreDirectory = true
                };

                if (loadFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if ((fileStream = loadFileDialog.OpenFile()) != null)
                    {
                        try
                        {
                            byte[] bytes = new byte[fileStream.Length];
                            fileStream.Read(bytes, 0, bytes.Length);
                            gridToAdd = new SerializedGrid(Encoding.UTF8.GetString(bytes));
                            lastFileDirectory = null;
                        }
                        catch(InvalidCastException) // serialization failed
                        {
                            MessageBox.Show("Не удалось открыть файл проекта. Неверный формат", "Произошла ошибка при открытии файла");
                            gridLoaded = false;
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Проверьте, присутствует ли Newtonsoft.Json.dll в папке программы", "Произошла ошибка при открытии файла");
                            gridLoaded = false;
                        }
                        finally
                        {
                            fileStream.Close();
                        }
                        gridLoaded = true;
                    }
                }
            };
            loadItem.ShortcutKeys = Keys.Control | Keys.D;
            fileItem.DropDownItems.Add(loadItem);

            MenuItems.Items.Add(fileItem);


            ToolStripMenuItem settingsItem = new ToolStripMenuItem("Settings");

            var updateItem = new ToolStripMenuItem("Auto-update")
            {
                Checked = true
            };
            updateItem.Click += (object o, EventArgs args) =>
            {
                var element = (ToolStripMenuItem)o;
                element.Checked = updateEnabled = !element.Checked;
            };
            updateItem.ShortcutKeys = Keys.Control | Keys.U;
            settingsItem.DropDownItems.Add(updateItem);


            var drawLinesItem = new ToolStripMenuItem("Grid")
            {
                Checked = true
            };
            drawLinesItem.Click += (object o, EventArgs args) =>
            {
                var element = (ToolStripMenuItem)o;
                element.Checked = drawLines = !element.Checked;
                mainGrid.Invalidate();
            };
            drawLinesItem.ShortcutKeys = Keys.Control | Keys.G;
            settingsItem.DropDownItems.Add(drawLinesItem);


            var gridResizeItem = new ToolStripMenuItem("Size");
            var textbox = new ToolStripTextBox("Enter Sizes:")
            {
                Text = "Xsize Ysize"
            };
            textbox.LostFocus += (object o, EventArgs args) =>
            {
                string[] sizes = textbox.Text.Split(";, ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    int xsize = int.Parse(sizes[0]);
                    int ysize = int.Parse(sizes[1]);
                    grid = grid.Resize(xsize, ysize);
                    ChangeScale(scale);
                }
                catch (Exception) { }
            };
            gridResizeItem.DropDownItems.Add(textbox);
            settingsItem.DropDownItems.Add(gridResizeItem);

            var clearItem = new ToolStripMenuItem("Clear")
            {
                ShortcutKeys = Keys.Control | Keys.C
            };
            clearItem.Click += (object o, EventArgs args) => { grid.Clear(); mainGrid.Invalidate(); };
            settingsItem.DropDownItems.Add(clearItem);

            var shrinkItem = new ToolStripMenuItem("Shrink")
            {
                ShortcutKeys = Keys.Control | Keys.H
            };
            shrinkItem.Click += (object o, EventArgs args) => { grid = grid.Shrink(); ChangeScale(scale); };
            settingsItem.DropDownItems.Add(shrinkItem);

            MenuItems.Items.Add(settingsItem);

            var speeditem = new ToolStripMenuItem("Speed");

            for (int i = 1; i < 9; i++)
            {
                int k = 1000 / (int)Math.Pow(2, i);
                var speedOption = new ToolStripMenuItem(k.ToString() + " ms");
                speedOption.Click += (object o, EventArgs args) => mainTimer.Interval = k;
                speeditem.DropDownItems.Add(speedOption);
            }
            MenuItems.Items.Add(speeditem);

            var scaleGridSettings = new ToolStripMenuItem("Scale");

            for (int i = 1; i < 8; i++)
            {
                int k = i;
                var scaleOption = new ToolStripMenuItem(Math.Round((100.0 / Math.Pow(2, i - 1)), 1).ToString() + '%');
                scaleOption.Click += (object o, EventArgs args) => ChangeScale(k);
                scaleGridSettings.DropDownItems.Add(scaleOption);
            }

            MenuItems.Items.Add(scaleGridSettings);
            formLoaded = true;
        }

        private void OnMainGridPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int cellSize = defaultCellSize / scale;
            Pen p = new Pen(Color.LightGray);

            drawer.DrawGrid(g, defaultCellSize, scale);

            drawer.DrawLabels(mainGrid, textLabelSize, defaultCellSize, scale);

            if (drawLines)
            {
                drawer.DrawLines(g, defaultCellSize, scale, p);
            }

            if (gridLoaded && gridToAdd != null)
            {
                GridDrawer.DrawLines(g, defaultCellSize, scale, new Pen(Color.Red, 2), gridToAdd.xsize, gridToAdd.ysize, loadX, loadY);
            }
        }

        private void SaveAsFile(object sender, EventArgs e)
        {
            Stream fileStream;
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.Filter = "(*.elc)|*.elc";
            saveFileDialog.FilterIndex = 2;
            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                if ((fileStream = saveFileDialog.OpenFile()) != null)
                {
                    try
                    {
                        byte[] bytes = Encoding.ASCII.GetBytes(new SerializedGrid(grid).GetJsonSerialization());
                        fileStream.Write(bytes, 0, bytes.Length);
                        lastFileDirectory = saveFileDialog.FileName;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Проверьте, присутствует ли Newtonsoft.Json.dll в папке программы", "Произошла ошибка при сохранении файла");
                    }
                    finally
                    {
                        fileStream.Close();
                    }
                }
                fileSaved = true;
            }
        }

        private void SaveFile(object sender, EventArgs e)
        {
            if (!File.Exists(lastFileDirectory))
            {
                SaveAsFile(sender, e);
            }
            else
            {
                try
                {
                    using (Stream fileStream = File.Open(lastFileDirectory, FileMode.Create))
                    {
                        byte[] bytes = Encoding.ASCII.GetBytes(new SerializedGrid(grid).GetJsonSerialization());
                        fileStream.Write(bytes, 0, bytes.Length);
                        fileStream.Close();
                        fileSaved = true;
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Проверьте, присутствует ли Newtonsoft.Json.dll в папке программы", "Произошла ошибка при сохранении файла");
                }
            }
        }

        private void OnMainGridMouseDown(object sender, MouseEventArgs e)
        {
            fileSaved = false;
            mousePressed = true;

            if (e.Button == MouseButtons.Right)
            {
                gridToAdd = null;
                gridLoaded = false;
            }

            int x = e.X * scale;
            int y = e.Y * scale;
            x -= x % defaultCellSize;
            y -= y % defaultCellSize;
            if (x >= (grid.xsize * defaultCellSize) || y >= (grid.ysize * defaultCellSize)) return;

            if (gridLoaded && gridToAdd != null)
            {
                gridLoaded = false;
                PlaceGridOnField(x, y);
                gridToAdd = null;
                return;
            }

            IElement element = null;
            if (drawObjectType.IsSubclassOf(typeof(BaseElement)) && e.Button != MouseButtons.Right)
            {
                element = (IElement)Activator.CreateInstance(drawObjectType);
            }

            int gridX = e.X / (defaultCellSize / scale);
            int gridY = e.Y / (defaultCellSize / scale);

            if (gridX < 0 || gridX >= grid.xsize || gridY < 0 || gridY >= grid.ysize) return;

            if(drawObjectType == typeof(Eraser) || e.Button == MouseButtons.Right)
            {
                grid.SetElement(null, gridX, gridY); // delete element at [gridX, gridY]
            }
            else if (grid.elements[gridX, gridY] != null) // if element exists - click on it
            {
                if (drawObjectType == typeof(TextLabeler)) // add text to element if supported
                {
                    BaseElement el = grid.elements[gridX, gridY];
                    if (el.SupportsLabeling)
                    {
                        string text = string.Empty;
                        text = CreateInputBox();
                        el.Name = text;
                    }
                }
                else // if element exists - click on it
                {
                    grid.elements[gridX, gridY].Click();
                }
            }
            else // set new element at [gridX, gridY]
            {
                grid.SetElement(element, gridX, gridY);
            }
            mainGrid.Invalidate();
        }

        private string CreateInputBox()
        {
            mousePressed = false;
            DialogForm InputDialog = new DialogForm();
            string text = string.Empty;

            if (InputDialog.ShowDialog() == DialogResult.OK)
            {
                text = InputDialog.Input;
            }
            InputDialog.Dispose();

            return text;
        }

        void ChangeScale(int newScale)
        {
            scale = newScale;

            drawer?.Dispose(mainGrid);
            drawer = new GridDrawer(grid);
            drawer.AddFolder(resourcePath, defaultCellSize, scale);

            mainGrid.Invalidate();
        }

        private void OnElementSelectButtonClick(object sender, EventArgs e)
        {
            drawObjectType = Type.GetType("Electronics." + (string)((ButtonBase)sender).Tag);
        }

        private void OnMainTimerTick(object sender, EventArgs e)
        {
            if (updateEnabled)
            {
                grid.Update();
            }
            mainGrid.Invalidate();
        }

        private void OnMainGridMouseMove(object sender, MouseEventArgs e)
        {
            loadX = e.X;
            loadY = e.Y;
            loadX -= loadX % (defaultCellSize / scale);
            loadY -= loadY % (defaultCellSize / scale);

            if(mousePressed)
            {
                OnMainGridMouseDown(null, e);
            }

            mainGrid.Invalidate();
        }

        private void OnAppClose(object sender, FormClosingEventArgs e)
        {
            if (!fileSaved && formLoaded)
            {
                SaveAsFile(sender, e);
            }
        }

        private void OnMainGridMouseUp(object sender, MouseEventArgs e)
        {
            mousePressed = false;
        }

        private void PlaceGridOnField(int x, int y)
        {
            int xPos = x / defaultCellSize, yPos = y / defaultCellSize;

            grid = grid.Resize(Math.Max(xPos + gridToAdd.xsize, grid.xsize), Math.Max(yPos + gridToAdd.ysize, grid.ysize));
            ChangeScale(scale);

            for (int i = 0; i < gridToAdd.xsize; i++)
            {
                for (int j = 0; j < gridToAdd.ysize; j++)
                {
                    BaseElement element = null;
                    if (gridToAdd.elements[i, j] != null)
                    {
                        element = (BaseElement)Activator.CreateInstance(Type.GetType("Electronics." + gridToAdd.elements[i, j].Type));
                        element.Name = gridToAdd.elements[i, j].Name;
                    }
                    grid.SetElement(element, xPos + i, yPos + j);
                }
            }
        }
    }
}
