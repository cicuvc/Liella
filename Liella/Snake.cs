using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Liella.Game0 {
    unsafe struct FrameBuffer {
        public const int Width = 40;
        public const int Height = 20;
        public const int Area = Width * Height;

        fixed char _chars[Area];

        public void SetPixel(int x, int y, char character) {
            _chars[y * Width + x] = character;
        }

        public void Clear() {
            for (int i = 0; i < Area; i++)
                _chars[i] = ' ';
        }

        public readonly void Render() {
            Console.SetCursorPosition(0, 0);

            const ConsoleColor snakeColor = ConsoleColor.Green;

            Console.ForegroundColor = snakeColor;

            for (int i = 1; i <= Area; i++) {
                char c = _chars[i - 1];

                if (c == '*' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) {
                    Console.ForegroundColor = c == '*' ? ConsoleColor.Red : ConsoleColor.White;
                    Console.Write(c);
                    Console.ForegroundColor = snakeColor;
                } else
                    Console.Write(c);

                if (i % Width == 0) {
                    Console.SetCursorPosition(0, i / Width - 1);
                }
            }
        }
    }
    struct Random {
        private uint _val;

        public Random(uint seed) {
            _val = seed;
        }

        public uint Next() => _val = (1103515245 * _val + 12345) % 2147483648;
    }
    struct Snake {
        public const int MaxLength = 30;

        private int _length;

        // 身体是一个打包的整数，打包了X坐标、Y坐标和字符。
        // 为蛇的身体。
        // 只有原始类型可以使用C#的`固定`，因此这是一个`int`。
        private unsafe fixed int _body[MaxLength];

        private Direction _direction;
        private Direction _oldDirection;

        public Direction Course {
            set {
                if (_oldDirection != _direction)
                    _oldDirection = _direction;

                if (_direction - value != 2 && value - _direction != 2)
                    _direction = value;
            }
        }

        public unsafe Snake(byte x, byte y, Direction direction) {
            _body[0] = new Part(x, y, DirectionToChar(direction, direction)).Pack();
            _direction = direction;
            _oldDirection = direction;
            _length = 1;
        }

        public unsafe bool Update() {
            Part oldHead = Part.Unpack(_body[0]);
            Part newHead = new Part(
                (byte)(_direction switch {
                    Direction.Left => oldHead.X == 0 ? FrameBuffer.Width - 1 : oldHead.X - 1,
                    Direction.Right => (oldHead.X + 1) % FrameBuffer.Width,
                    _ => oldHead.X,
                }),
                (byte)(_direction switch {
                    Direction.Up => oldHead.Y == 0 ? FrameBuffer.Height - 1 : oldHead.Y - 1,
                    Direction.Down => (oldHead.Y + 1) % FrameBuffer.Height,
                    _ => oldHead.Y,
                }),
                DirectionToChar(_direction, _direction)
                );

            oldHead = new Part(oldHead.X, oldHead.Y, DirectionToChar(_oldDirection, _direction));

            bool result = true;

            for (int i = 0; i < _length - 1; i++) {
                Part current = Part.Unpack(_body[i]);
                if (current.X == newHead.X && current.Y == newHead.Y)
                    result = false;
            }

            _body[0] = oldHead.Pack();

            for (int i = _length - 2; i >= 0; i--) {
                _body[i + 1] = _body[i];
            }

            _body[0] = newHead.Pack();

            _oldDirection = _direction;

            return result;
        }

        public unsafe readonly void Draw(ref FrameBuffer fb) {
            for (int i = 0; i < _length; i++) {
                Part p = Part.Unpack(_body[i]);
                fb.SetPixel(p.X, p.Y, p.Character);
            }
        }

        public bool Extend() {
            if (_length < MaxLength) {
                _length += 1;
                return true;
            }
            return false;
        }

        public unsafe readonly bool HitTest(int x, int y) {
            for (int i = 0; i < _length; i++) {
                Part current = Part.Unpack(_body[i]);
                if (current.X == x && current.Y == y)
                    return true;
            }

            return false;
        }

        private static char DirectionToChar(Direction oldDirection, Direction newDirection) {
            const string DirectionChangeToChar = "│┌?┐┘─┐??└│┘└?┌─";
            return DirectionChangeToChar[(int)oldDirection * 4 + (int)newDirection];
        }

        // 帮助结构来打包和解压_body中打包的整数。
        readonly struct Part {
            public readonly byte X, Y;
            public readonly char Character;

            public Part(byte x, byte y, char c) {
                X = x;
                Y = y;
                Character = c;
            }

            public int Pack() => X << 24 | Y << 16 | Character;
            public static Part Unpack(int packed) => new Part((byte)(packed >> 24), (byte)(packed >> 16), (char)packed);
        }

        public enum Direction {
            Up, Right, Down, Left
        }
    }
    struct Game {
        enum Result {
            Win, Loss
        }

        private Random _random;

        private Game(uint randomSeed) {
            _random = new Random(randomSeed);
        }

        private Result Run(ref FrameBuffer fb) {
            Snake s = new Snake(
                (byte)(_random.Next() % FrameBuffer.Width),
                (byte)(_random.Next() % FrameBuffer.Height),
                (Snake.Direction)(_random.Next() % 4));

            MakeFood(s, out byte foodX, out byte foodY);

            long gameTime = Environment.TickCount64;

            while (true) {
                fb.Clear();

                if (!s.Update()) {
                    s.Draw(ref fb);
                    return Result.Loss;
                }

                s.Draw(ref fb);

                if (Console.KeyAvailable) {
                    ConsoleKeyInfo ki = Console.ReadKey(intercept: true);
                    switch (ki.Key) {
                        case ConsoleKey.UpArrow:
                            s.Course = Snake.Direction.Up; break;
                        case ConsoleKey.DownArrow:
                            s.Course = Snake.Direction.Down; break;
                        case ConsoleKey.LeftArrow:
                            s.Course = Snake.Direction.Left; break;
                        case ConsoleKey.RightArrow:
                            s.Course = Snake.Direction.Right; break;
                    }
                }

                if (s.HitTest(foodX, foodY)) {
                    if (s.Extend())
                        MakeFood(s, out foodX, out foodY);
                    else
                        return Result.Win;
                }

                fb.SetPixel(foodX, foodY, '*');

                fb.Render();

                gameTime += 100;

                long delay = gameTime - Environment.TickCount64;
                if (delay >= 0)
                    Thread.Sleep((int)delay);
                else
                    gameTime = Environment.TickCount64;
            }
        }

        void MakeFood(in Snake snake, out byte foodX, out byte foodY) {
            do {
                foodX = (byte)(_random.Next() % (FrameBuffer.Width - 1));
                foodY = (byte)(_random.Next() % (FrameBuffer.Height - 1));
            }
            while (snake.HitTest(foodX, foodY));
        }

        static void Main() {
            Console.SetWindowSize(FrameBuffer.Width, FrameBuffer.Height);
            Console.SetBufferSize(FrameBuffer.Width, FrameBuffer.Height);
            Console.Title = "See Sharp Snake";
            Console.CursorVisible = false;

            FrameBuffer fb = new FrameBuffer();

            while (true) {
                Game g = new Game((uint)Environment.TickCount64);
                Result result = g.Run(ref fb);

                string message = result == Result.Win ? "You win" : "You lose";

                int position = (FrameBuffer.Width - message.Length) / 2;
                for (int i = 0; i < message.Length; i++) {
                    fb.SetPixel(position + i, FrameBuffer.Height / 2, message[i]);
                }

                fb.Render();

                Console.ReadKey(intercept: true);
            }
        }
    }
}
