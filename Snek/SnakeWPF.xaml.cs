using Snek.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace Snek
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class SnakeWPF : Window, INotifyPropertyChanged
    {
        private static string highscorefilepath = @"snake_highscorelist.xml";
        public SnakeWPF()
        {
            InitializeComponent();
            gameTickTimer.Tick += GameTickTimer_Tick;
            LoadHighscoreList();
        }

        private void GameTickTimer_Tick(object sender, EventArgs e)
        {
            MoveSnake();
        }

        #region Properties
        const int SnakeSquareSize = 20;
        const int SnakeStartLength = 3;
        const int SnakeStartSpeed = 400;
        const int SnakeSpeedTreshold = 100;
        const int MaxHighscoreListEntryCount = 5;
        public bool DeathByWalls { get; set; } = false;
        private bool hasMoved = true;

        private readonly Random rnd = new Random();
        private UIElement snakeFood = null;
        private readonly SolidColorBrush foodBrush = Brushes.Red;

        private readonly SolidColorBrush snakeBodyBrush = Brushes.Gold;
        private readonly SolidColorBrush snakeHeadBrush = Brushes.YellowGreen;
        private readonly List<SnakePart> snakeParts = new List<SnakePart>();

        public enum SnakeDirection { Left, Right, Up, Down };
        private SnakeDirection snakeDir = SnakeDirection.Right;
        private int snakeLength;

        private string _score = "Loading...";
        public string ScoreDisplay { get { return _score; } set { if (_score != value) { _score = value; OnPropertyChanged(nameof(ScoreDisplay)); } } }
        private int _currentScore;
        public int CurrentScore { get { return _currentScore; } set { if (_currentScore != value) { _currentScore = value; ScoreDisplay = "Score: " + value.ToString(); } } }

        private string _speed = "Loading...";
        public string SpeedDisplay { get { return _speed; } set { if (_speed != value) { _speed = value; OnPropertyChanged(nameof(SpeedDisplay)); } } }
        public double CurrentSpeed { get { return gameTickTimer.Interval.TotalMilliseconds; } set { if (gameTickTimer.Interval.TotalMilliseconds != value) { gameTickTimer.Interval = TimeSpan.FromMilliseconds(value); SpeedDisplay = "Speed: " + gameTickTimer.Interval.TotalMilliseconds.ToString(); } } }
        
        private readonly System.Windows.Threading.DispatcherTimer gameTickTimer = new System.Windows.Threading.DispatcherTimer();

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        protected void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            DrawGameArea();
            ScoreDisplay = "Score: 0";
            SpeedDisplay = "Speed: 0";
        }

        private void DrawGameArea()
        {
            bool doneDrawing = false;
            int nxtX = 0, nxtY = 0;
            int rwcnt = 0;
            bool nxtOdd = false;

            while (!doneDrawing)
            {
                Rectangle rect = new Rectangle
                {
                    Width = SnakeSquareSize,
                    Height = SnakeSquareSize,
                    Fill = nxtOdd ? Brushes.White : Brushes.Black
                };
                GameArea.Children.Add(rect);
                Canvas.SetTop(rect, nxtX);
                Canvas.SetLeft(rect, nxtY);

                nxtOdd = !nxtOdd;
                nxtX += SnakeSquareSize;
                if (nxtX >= GameArea.ActualWidth)
                {
                    nxtX = 0;
                    nxtY += SnakeSquareSize;
                    rwcnt++;
                    nxtOdd = (rwcnt % 2 != 0);
                }

                if (nxtY >= GameArea.ActualHeight)
                {
                    doneDrawing = true;
                }
            }
        }

        private void DrawSnake()
        {
            foreach (SnakePart snkpart in snakeParts)
            {
                if (snkpart.UiElement == null)
                {
                    snkpart.UiElement = new Rectangle()
                    {
                        Width = SnakeSquareSize,
                        Height = SnakeSquareSize,
                        Fill = (snkpart.IsHead ? snakeHeadBrush : snakeBodyBrush)
                    };
                    GameArea.Children.Add(snkpart.UiElement);
                    Canvas.SetTop(snkpart.UiElement, snkpart.Position.Y);
                    Canvas.SetLeft(snkpart.UiElement, snkpart.Position.X);
                }
            }
        }

        private void MoveSnake()
        {
            while (snakeParts.Count >= snakeLength)
            {
                GameArea.Children.Remove(snakeParts[0].UiElement);
                snakeParts.RemoveAt(0);
            }
            foreach (SnakePart snkprt in snakeParts)
            {
                (snkprt.UiElement as Rectangle).Fill = snakeBodyBrush;
                snkprt.IsHead = false;
            }
            SnakePart snkhead = snakeParts[snakeParts.Count - 1];
            double nxtX = snkhead.Position.X;
            double nxtY = snkhead.Position.Y;
            switch (snakeDir)
            {

                case SnakeDirection.Left:
                    nxtX -= SnakeSquareSize;
                    break;
                case SnakeDirection.Right:
                    nxtX += SnakeSquareSize;
                    break;
                case SnakeDirection.Up:
                    nxtY -= SnakeSquareSize;
                    break;
                case SnakeDirection.Down:
                    nxtY += SnakeSquareSize;
                    break;
            }
            if (!DeathByWalls)
            {
                if (nxtX < 0)
                    nxtX = (int)(GameArea.ActualWidth);
                else if (nxtY < 0)
                    nxtY = (int)(GameArea.ActualHeight);
                else if (nxtX >= GameArea.ActualWidth)
                    nxtX = 0;
                else if (nxtY >= GameArea.ActualHeight)
                    nxtY = 0;
            }
            snakeParts.Add(new SnakePart()
            {
                Position = new Point(nxtX, nxtY),
                IsHead = true
            });
            DrawSnake();
            DoCollisionCheck();
            gameTickTimer.Interval = TimeSpan.FromMilliseconds(gameTickTimer.Interval.TotalMilliseconds);
            hasMoved = true;
        }

        private void StartNewGame()
        {
            //init
            bdrWelcomeMessage.Visibility = Visibility.Collapsed;
            bdrHighscoreList.Visibility = Visibility.Collapsed;
            bdrEndOfGame.Visibility = Visibility.Collapsed;

            //cleanup
            foreach (SnakePart snakeBodyPart in snakeParts)
            {
                if (snakeBodyPart.UiElement != null)
                {
                    GameArea.Children.Remove(snakeBodyPart.UiElement);
                }
            }
            snakeParts.Clear();
            if (snakeFood != null) GameArea.Children.Remove(snakeFood);

            //default values
            CurrentScore = 0;
            snakeLength = SnakeStartLength;
            snakeDir = SnakeDirection.Right;
            snakeParts.Add(new SnakePart() { Position = new Point(SnakeSquareSize * 5, SnakeSquareSize * 5) });
            CurrentSpeed = SnakeStartSpeed;
            ScoreDisplay = "Score: 0";

            //methods
            DrawSnake();
            DrawSnakeFood();
            gameTickTimer.IsEnabled = true;
        }

        private Point GetNextFoodPosition()
        {
            int maxX = (int)(GameArea.ActualWidth / SnakeSquareSize);
            int maxY = (int)(GameArea.ActualHeight / SnakeSquareSize);
            int foodX = rnd.Next(0, maxX) * SnakeSquareSize;
            int foodY = rnd.Next(0, maxY) * SnakeSquareSize;

            foreach (SnakePart snakePart in snakeParts)
            {
                if ((snakePart.Position.X == foodX) && (snakePart.Position.Y == foodY))
                {
                    return GetNextFoodPosition();
                }
            }

            return new Point(foodX, foodY);
        }

        private void DrawSnakeFood()
        {
            Point foodPos = GetNextFoodPosition();
            snakeFood = new Ellipse()
            {
                Width = SnakeSquareSize,
                Height = SnakeSquareSize,
                Fill = foodBrush
            };
            GameArea.Children.Add(snakeFood);
            Canvas.SetTop(snakeFood, foodPos.Y);
            Canvas.SetLeft(snakeFood, foodPos.X);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (!hasMoved && e.Key != Key.Space && bdrNewHighscore.Visibility != Visibility.Visible ) return;

            SnakeDirection origDir = snakeDir;
            switch (e.Key)
            {
                case Key.Up: if (snakeDir != SnakeDirection.Down) snakeDir = SnakeDirection.Up; break;
                case Key.Down: if (snakeDir != SnakeDirection.Up) snakeDir = SnakeDirection.Down; break;
                case Key.Left: if (snakeDir != SnakeDirection.Right) snakeDir = SnakeDirection.Left; break;
                case Key.Right: if (snakeDir != SnakeDirection.Left) snakeDir = SnakeDirection.Right; break;
                case Key.Space: StartNewGame(); break;
            }
            hasMoved = false;
            if (snakeDir != origDir) MoveSnake();
        }

        private void DoCollisionCheck()
        {
            SnakePart snakeHead = snakeParts[snakeParts.Count - 1];
            if ((snakeHead.Position.X == Canvas.GetLeft(snakeFood)) && (snakeHead.Position.Y == Canvas.GetTop(snakeFood)))
            {
                EatSnakeFood();
                return;
            }
            if (DeathByWalls)
            {
                if ((snakeHead.Position.Y < 0) || (snakeHead.Position.Y >= GameArea.ActualHeight) ||
                    (snakeHead.Position.X < 0) || (snakeHead.Position.X >= GameArea.ActualWidth))
                {
                    EndGame();
                }
            }
            foreach (SnakePart snakeBodyPart in snakeParts.Take(snakeParts.Count - 1))
            {
                if ((snakeHead.Position.X == snakeBodyPart.Position.X) && snakeHead.Position.Y == snakeBodyPart.Position.Y)
                {
                    EndGame();
                }
            }
        }

        private void EatSnakeFood()
        {
            snakeLength++;
            CurrentScore++;
            int timerInterval = Math.Max(SnakeSpeedTreshold, (int)gameTickTimer.Interval.TotalMilliseconds - (_currentScore * 2));
            CurrentSpeed = timerInterval;
            GameArea.Children.Remove(snakeFood);
            DrawSnakeFood();
        }

        private void EndGame()
        {
            bool isNewHighscore = false;
            if (CurrentScore > 0)
            {
                int lowestHighscore = (this.HighscoreList.Count > 0 ? this.HighscoreList.Min(x => x.Score) : 0);
                if ((CurrentScore > lowestHighscore) || (this.HighscoreList.Count < MaxHighscoreListEntryCount))
                {
                    bdrNewHighscore.Visibility = Visibility.Visible;
                    txtPlayerName.Focus();
                    isNewHighscore = true;
                }
            }
            if (!isNewHighscore)
            {
                tbFinalScore.Text = ScoreDisplay;
                bdrEndOfGame.Visibility = Visibility.Visible;
            }
            gameTickTimer.IsEnabled = false;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnShowHighscoreList_Click(object sender, RoutedEventArgs e)
        {
            bdrWelcomeMessage.Visibility = Visibility.Collapsed;
            bdrHighscoreList.Visibility = Visibility.Visible;
        }

        public ObservableCollection<SnakeHighscore> HighscoreList
        {
            get; set;
        } = new ObservableCollection<SnakeHighscore>();

        private void LoadHighscoreList()
        {
            if (File.Exists(highscorefilepath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<SnakeHighscore>));
                using (Stream reader = new FileStream(highscorefilepath, FileMode.Open))
                {
                    List<SnakeHighscore> tempList = (List<SnakeHighscore>)serializer.Deserialize(reader);
                    this.HighscoreList.Clear();
                    foreach (var item in tempList.OrderByDescending(x => x.Score)) this.HighscoreList.Add(item);
                }
            }
        }

        private void SaveHighscoreList()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<SnakeHighscore>));
            using (Stream writer = new FileStream(highscorefilepath, FileMode.Create))
            {
                serializer.Serialize(writer, this.HighscoreList);
            }
        }

        private void BtnAddToHighscoreList_Click(object sender, RoutedEventArgs e)
        {
            int newIndex = 0;
            if ((this.HighscoreList.Count > 0) && (CurrentScore < this.HighscoreList.Max(x => x.Score)))
            {
                SnakeHighscore justAbove = this.HighscoreList.OrderByDescending(x => x.Score).First(x => x.Score >= CurrentScore);
                if (justAbove != null) newIndex = this.HighscoreList.IndexOf(justAbove) + 1;
            }
            this.HighscoreList.Insert(newIndex, new SnakeHighscore()
            {
                PlayerName = txtPlayerName.Text,
                Score = CurrentScore
            });
            while (this.HighscoreList.Count > MaxHighscoreListEntryCount) this.HighscoreList.RemoveAt(MaxHighscoreListEntryCount);
            SaveHighscoreList();
            bdrNewHighscore.Visibility = Visibility.Collapsed;
            bdrHighscoreList.Visibility = Visibility.Visible;
        }
    }
}
