using System;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Shadertoy
{
    class ShaderWindow : GameWindow
    {
        #region --- Fields ---

        float globalTime = 0.0f, timeSpeed = 1.0f;
        int vertex_shader_object, fragment_shader_object, shader_program;
        int vertex_buffer_object, color_buffer_object, element_buffer_object;

        //Shapes.Shape shape = new Examples.Shapes.Cube();
        Vector3[] RectVertices = new Vector3[] {   new Vector3(-1.0f, -1.0f,  0.0f), new Vector3( 1.0f, -1.0f,  0.0f),
                                                    new Vector3( 1.0f,  1.0f,  0.0f), new Vector3(-1.0f,  1.0f,  0.0f) };
        int[] RectIndices = new int[] { 0, 1, 2, 2, 3, 0 };
        private static int ColorToRgba32(Color c) { return (int)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R); }
        int[] RectColors = new int[] { ColorToRgba32(Color.DarkRed), ColorToRgba32(Color.DarkRed), ColorToRgba32(Color.Gold), ColorToRgba32(Color.Gold) };

        private Vector3 cameraPosition = new Vector3(3.5f, 1.7f, 6.0f); // Начальная позиция камеры
        private float cameraSpeed = 0.1f; // Скорость камеры
        private float HalfSizePool = 2.0f; // Начальное значение
        private float DepthPool = 2;

        private float BallSize = 0.75f;
        private Vector3 LightPos = new Vector3(2.0f, 1.5f, 0.0f);
        private Vector3 WaterNumber = new Vector3(0.4f, 0.9f, 1.0f);

        private bool isMenuShown = false; // Флаг для контроля создания меню

        #endregion

        #region --- Shaders ---

        private string vertexShaderSource = @"
        void main()
        {
            gl_FrontColor = gl_Color;
            gl_Position = ftransform();
        }";

        // iResolution - viewport resolution (in pixels)
        // iGlobalTime - shader playback time (in seconds)
        private string fragmentShaderPrefix = @"
        #version 120
        uniform vec3 iResolution;
        uniform float iGlobalTime;
        ";

        public static string FragmentShaderExample = @"
        void main(void)
        {
	        vec2 uv = gl_FragCoord.xy / iResolution.xy;
	        gl_FragColor = vec4(uv,0.5+0.5*sin(iGlobalTime),1.0);
        }";
        public static string[] FragmentShaderSource;


        #endregion

        #region --- Constructors ---

        //public ShaderWindow() : base(800, 600, GraphicsMode.Default)
        public ShaderWindow() : base(512, 288, GraphicsMode.Default)
        {
        }



        #endregion

        #region OnLoad
        
        /// <summary>
        /// This is the place to load resources that change little
        /// during the lifetime of the GameWindow. In this case, we
        /// check for GLSL support, and load the shaders.
        /// </summary>
        /// <param name="e">Not used.</param>
        /// 

        /*Метод OnLoad вызывается при загрузке окна. Здесь происходит:
        Проверка версии OpenGL для обеспечения совместимости.
        Установка цвета фона и включение тестирования глубины.
        Создание вершинных буферов (VBO) и загрузка шейдеров.*/
        protected override void OnLoad(EventArgs e)
        {
            //CursorVisible = false;
            WindowState = WindowState.Maximized;
            ///example.WindowBorder = OpenTK.WindowBorder.Hidden;
            ShowMenu();

            base.OnLoad(e);

            // Check for necessary capabilities:
            Version version = new Version(GL.GetString(StringName.Version).Substring(0, 3));
            Version target = new Version(2, 0);
            if (version < target)
            {
                throw new NotSupportedException(String.Format(
                    "OpenGL {0} is required (you only have {1}).", target, version));
            }

            GL.ClearColor(Color.MidnightBlue);
            GL.Enable(EnableCap.DepthTest);

            CreateVBO();

            //using (StreamReader vs = new StreamReader("Data/Shaders/Simple_VS.glsl"))
            //using (StreamReader fs = new StreamReader("Data/Shaders/Simple_FS.glsl"))
            string fragmentShader = GetFragmentShaderStr(FragmentShaderSource);
            CreateShaders(vertexShaderSource, fragmentShaderPrefix + fragmentShader, out vertex_shader_object, out fragment_shader_object, out shader_program);
        }

        private string GetFragmentShaderStr(string[] shaderSource)
        {
            string sum = "";
            foreach (string line in shaderSource)
            {
                int i = line.IndexOf("//");
                string newLine = i > -1 ? line.Remove(i) : line;
                sum += newLine + "\n";
            }
            return sum;
        }

        #endregion

        #region CreateShaders

        /*Метод CreateShaders компилирует и связывает вершинные и фрагментные шейдеры, обрабатывая возможные ошибки компиляции. Этот процесс включает:
        Создание объектов шейдеров.
        Загрузка исходного кода шейдеров.
        Компиляция и проверка статуса компиляции.*/
        void CreateShaders(string vs, string fs, out int vertexObject, out int fragmentObject, out int program)
        {
            int status_code;
            string info;

            vertexObject = GL.CreateShader(ShaderType.VertexShader);
            fragmentObject = GL.CreateShader(ShaderType.FragmentShader);

            // Compile vertex shader
            GL.ShaderSource(vertexObject, vs);
            GL.CompileShader(vertexObject);
            GL.GetShaderInfoLog(vertexObject, out info);
            GL.GetShader(vertexObject, ShaderParameter.CompileStatus, out status_code);

            if (status_code != 1)
                throw new ApplicationException(info);

            // Compile vertex shader
            GL.ShaderSource(fragmentObject, fs);
            GL.CompileShader(fragmentObject);
            GL.GetShaderInfoLog(fragmentObject, out info);
            GL.GetShader(fragmentObject, ShaderParameter.CompileStatus, out status_code);

            if (status_code != 1)
                throw new ApplicationException(info);

            program = GL.CreateProgram();
            GL.AttachShader(program, fragmentObject);
            GL.AttachShader(program, vertexObject);

            GL.LinkProgram(program);
            GL.UseProgram(program);
        }

        #endregion

        #region CreateVBO()

        /*Метод CreateVBO отвечает за создание и загрузку данных в вершинные буферы. Он включает:
        Генерацию буферов для вершин, цветов и индексов.
        Загрузку данных в GPU с помощью методов OpenGL, таких как GL.BufferData.*/
        void CreateVBO()
        {
            int size;

            GL.GenBuffers(1, out vertex_buffer_object);
            GL.GenBuffers(1, out color_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);

            // Upload the vertex buffer.
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(RectVertices.Length * 3 * sizeof(float)), RectVertices,
                BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize, out size);
            if (size != RectVertices.Length * 3 * sizeof(Single))
                throw new ApplicationException(String.Format(
                    "Problem uploading vertex buffer to VBO (vertices). Tried to upload {0} bytes, uploaded {1}.",
                    RectVertices.Length * 3 * sizeof(Single), size));

            // Upload the color buffer.
            GL.BindBuffer(BufferTarget.ArrayBuffer, color_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(RectColors.Length * sizeof(int)), RectColors,
                BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize, out size);
            if (size != RectColors.Length * sizeof(int))
                throw new ApplicationException(String.Format(
                    "Problem uploading vertex buffer to VBO (colors). Tried to upload {0} bytes, uploaded {1}.",
                    RectColors.Length * sizeof(int), size));

            // Upload the index buffer (elements inside the vertex buffer, not color indices as per the IndexPointer function!)
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(RectIndices.Length * sizeof(Int32)), RectIndices,
                BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize, out size);
            if (size != RectIndices.Length * sizeof(int))
                throw new ApplicationException(String.Format(
                    "Problem uploading vertex buffer to VBO (offsets). Tried to upload {0} bytes, uploaded {1}.",
                    RectIndices.Length * sizeof(int), size));
        }

        #endregion

        #region OnUnload

        protected override void OnUnload(EventArgs e)
        {
            if (shader_program != 0)
                GL.DeleteProgram(shader_program);
            if (fragment_shader_object != 0)
                GL.DeleteShader(fragment_shader_object);
            if (vertex_shader_object != 0)
                GL.DeleteShader(vertex_shader_object);
            if (vertex_buffer_object != 0)
                GL.DeleteBuffers(1, ref vertex_buffer_object);
            if (element_buffer_object != 0)
                GL.DeleteBuffers(1, ref element_buffer_object);
        }

        #endregion

        #region OnResize

        /// <summary>
        /// Called when the user resizes the window.
        /// </summary>
        /// <param name="e">Contains the new width/height of the window.</param>
        /// <remarks>
        /// You want the OpenGL viewport to match the window. This is the place to do it!
        /// </remarks>
        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);

            /*float aspect_ratio = Width / (float)Height;
            Matrix4 perpective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 1, 64);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perpective);*/
            Matrix4 ortho = Matrix4.CreateOrthographic(2, 2, 1, 64);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref ortho);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            Environment.Exit(0); // Завершение приложения
        }

        #endregion

        #region OnUpdateFrame

        /// <summary>
        /// Prepares the next frame for rendering.
        /// </summary>
        /// <remarks>
        /// Place your control logic here. This is the place to respond to user input,
        /// update object positions etc.
        /// </remarks>


        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            float deltaTime = (float)e.Time; // Time elapsed since last frame

            if (Keyboard[Key.Escape])
            {
                // Открываем меню
                ShowMenu();
            
            }

            if (Keyboard[Key.C])
            {
                Environment.Exit(0);
            }

            // Управление камерой с помощью клавиш WASD и стрелок
            if (Keyboard[Key.W]) cameraPosition.Z -= cameraSpeed;
            if (Keyboard[Key.S]) cameraPosition.Z += cameraSpeed;
            if (Keyboard[Key.A]) cameraPosition.X -= cameraSpeed;
            if (Keyboard[Key.D]) cameraPosition.X += cameraSpeed;
            if (Keyboard[Key.LControl]) cameraPosition.Y -= cameraSpeed;
            if (Keyboard[Key.Space]) cameraPosition.Y += cameraSpeed;

            // Управление значением HalfSizePool с помощью стрелок вверх и вниз
            if (Keyboard[Key.Q])
            {
                HalfSizePool += .1f; // Увеличиваем размер
            }
            if (Keyboard[Key.E])
            {
                if (HalfSizePool > 1)
                    HalfSizePool -= .1f; // Уменьшаем размер
            }

            if (Keyboard[Key.F])
            {
                DepthPool += .1f;
            }

            if (Keyboard[Key.R])
            {
                if (DepthPool > 1)
                    DepthPool -= .1f;
            }

            if (Keyboard[Key.Z])
            {
                BallSize += .05f;
            }

            if (Keyboard[Key.X])
            {
                if (BallSize > 0.2)
                    BallSize -= .05f;
            }

            // Управление первым значением LightPos
            if (Keyboard[Key.T] && !Keyboard[Key.AltLeft] && !Keyboard[Key.AltRight])
            {
                // Увеличить значение на 0.1
                LightPos.X += 0.1f;
            }

            else if (Keyboard[Key.T] && (Keyboard[Key.AltLeft] || Keyboard[Key.AltRight]))
            {
                // Уменьшить значение на 0.1
                LightPos.X -= 0.1f;
            }

            // Управление вторым значением LightPos
            if (Keyboard[Key.Y] && !Keyboard[Key.AltLeft] && !Keyboard[Key.AltRight])
            {
                // Увеличить значение на 0.1
                LightPos.Y += 0.1f;
            }

            else if (Keyboard[Key.Y] && (Keyboard[Key.AltLeft] || Keyboard[Key.AltRight]))
            {
                // Уменьшить значение на 0.1
                LightPos.Y -= 0.1f;
            }

            // Управление третьим значением LightPos
            if (Keyboard[Key.U] && !Keyboard[Key.AltLeft] && !Keyboard[Key.AltRight])
            {
                // Увеличить значение на 0.1
                LightPos.Z += 0.1f;
            }

            else if (Keyboard[Key.U] && (Keyboard[Key.AltLeft] || Keyboard[Key.AltRight]))
            {
                // Уменьшить значение на 0.1
                LightPos.Z -= 0.1f;
            }


            // Управление первым значением WaterNumber
            if (Keyboard[Key.G] && !Keyboard[Key.AltLeft] && !Keyboard[Key.AltRight])
            {
                // Увеличить значение на 0.1
                WaterNumber.X += 0.1f;
            }

            else if (Keyboard[Key.G] && (Keyboard[Key.AltLeft] || Keyboard[Key.AltRight]))
            {
                // Уменьшить значение на 0.1
                WaterNumber.X -= 0.1f;
            }

            // Управление вторым значением WaterNumber
            if (Keyboard[Key.H] && !Keyboard[Key.AltLeft] && !Keyboard[Key.AltRight])
            {
                // Увеличить значение на 0.1
                WaterNumber.Y += 0.1f;
            }

            else if (Keyboard[Key.H] && (Keyboard[Key.AltLeft] || Keyboard[Key.AltRight]))
            {
                // Уменьшить значение на 0.1
                WaterNumber.Y -= 0.1f;
            }

            // Управление третьим значением WaterNumber
            if (Keyboard[Key.J] && !Keyboard[Key.AltLeft] && !Keyboard[Key.AltRight])
            {
                // Увеличить значение на 0.1
                WaterNumber.Z += 0.1f;
            }

            else if (Keyboard[Key.J] && (Keyboard[Key.AltLeft] || Keyboard[Key.AltRight]))
            {
                // Уменьшить значение на 0.1
                WaterNumber.Z -= 0.1f;
            }
        }

        #endregion

        #region OnRenderFrame

        /// <summary>
        /// Place your rendering code here.
        /// </summary>
        /*Передача данных о позиции камеры и других uniform-переменных в шейдеры.
        Очистка буфера цвета и глубины перед отрисовкой.
        Настройка состояния OpenGL для работы с массивами вершин и цветов, а затем вызов метода отрисовки (GL.DrawElements).*/
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            // Передача позиции камеры в шейдер
            int cameraPositionLocation = GL.GetUniformLocation(shader_program, "iCameraPosition");
            GL.Uniform3(cameraPositionLocation, cameraPosition);

            // Теперь передаем обновленное значение HalfSizePool в шейдер
            // Получаем расположение uniform-переменной HalfSizePool в шейдере
            int halfSizePoolLocation = GL.GetUniformLocation(shader_program, "HalfSizePool");
            // Передаем значение HalfSizePool в шейдер
            GL.Uniform1(halfSizePoolLocation, HalfSizePool);

            int DepthPoolLocation = GL.GetUniformLocation(shader_program, "DepthPool");
            // Передаем значение HalfSizePool в шейдер
            GL.Uniform1(DepthPoolLocation, DepthPool);

            int BallSizeLocation = GL.GetUniformLocation(shader_program, "BallSize");
            // Передаем значение в шейдер
            GL.Uniform1(BallSizeLocation, BallSize);

            int LightPosLocation = GL.GetUniformLocation(shader_program, "LightPos");
            // Передаем значение в шейдер
            GL.Uniform3(LightPosLocation, LightPos.X, LightPos.Y, LightPos.Z);

            int WaterLocation = GL.GetUniformLocation(shader_program, "WaterNumber");
            // Передаем значение в шейдер
            GL.Uniform3(WaterLocation, WaterNumber.X, WaterNumber.Y, WaterNumber.Z);


            GL.Clear(ClearBufferMask.ColorBufferBit |
                     ClearBufferMask.DepthBufferBit);

            globalTime += timeSpeed * (float)e.Time;
            #region Uniforms
            // viewport resolution (in pixels) (window resopution)
            GL.Uniform3(GL.GetUniformLocation(shader_program, "iResolution"), (float)Width, (float)Height, 0.0f);
            // shader playback time (in seconds)
            GL.Uniform1(GL.GetUniformLocation(shader_program, "iGlobalTime"), (float)globalTime);
            #endregion Uniforms

            Matrix4 lookat = Matrix4.LookAt(0, 0, 2, 0, 0, 0, 0, 1, 0);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref lookat);


            //angle += rotation_speed * (float)e.Time;
            //GL.Rotate(angle, 0.0f, 1.0f, 0.0f);

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.ColorArray);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.VertexPointer(3, VertexPointerType.Float, 0, IntPtr.Zero);
            GL.BindBuffer(BufferTarget.ArrayBuffer, color_buffer_object);
            GL.ColorPointer(4, ColorPointerType.UnsignedByte, 0, IntPtr.Zero);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, element_buffer_object);

            GL.DrawElements(BeginMode.Triangles, RectIndices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //GL.DrawArrays(GL.Enums.BeginMode.POINTS, 0, shape.Vertices.Length);

            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.ColorArray);


            //int error = GL.GetError();
            //if (error != 0)
            //    Debug.Print(Glu.ErrorString(Glu.Enums.ErrorCode.INVALID_OPERATION));

            SwapBuffers();
        }

        #endregion

        #region Menu



        /*Методы ShowMenu и ShowCustomMessageBox создают простые формы Windows для взаимодействия с пользователем. 
        Они позволяют приостанавливать игру, показывать информацию о управлении и выходить из приложения.*/


        private void ShowMenu()
        {
            //Cursor.Hide();
            if (isMenuShown) return; // Проверка, чтобы не создавать меню повторно
            isMenuShown = true;

            Thread menuThread = new Thread(() =>
            {
                using (var menuForm = new Form())
                {
                    // Настройка формы
                    menuForm.Text = "Меню";
                    menuForm.Size = new Size(260, 280);
                    menuForm.StartPosition = FormStartPosition.Manual;
                    menuForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    menuForm.MaximizeBox = false;
                    menuForm.MinimizeBox = false;
                    menuForm.ControlBox = false;
                    menuForm.TopMost = true; // Поверх всех окон
                    menuForm.ShowInTaskbar = false; // Убираем из панели задач                                                                                      
                    

                    // Убираем из Alt+Tab
                    IntPtr handle = menuForm.Handle;
                    const int GWL_EXSTYLE = -20;
                    const int WS_EX_TOOLWINDOW = 0x00000080;

                    SetWindowLong(handle, GWL_EXSTYLE, GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
                    var screenBounds = Screen.PrimaryScreen.WorkingArea; // Область экрана без панели задач
                    menuForm.Location = new Point(0, 30);

                    // Кнопки
                    var continueButton = new Button
                    {
                        Text = "Закрыть меню",
                        Dock = DockStyle.Top,
                        Height = 60
                    };
                    continueButton.Click += (sender, args) => menuForm.Close();

                    var infoButton = new Button
                    {
                        Text = "Сведения",
                        Dock = DockStyle.Top,
                        Height = 60
                    };
                    infoButton.Click += (sender, args) => ShowCustomMessageBox();

                    var changeFieldButton = new Button
                    {
                        Text = "Изменить поле",
                        Dock = DockStyle.Top,
                        Height = 60
                    };
                    changeFieldButton.Click += (sender, args) => ChangeFields();

                    var exitButton = new Button
                    {
                        Text = "Выход",
                        Dock = DockStyle.Top,
                        Height = 60
                    };
                    exitButton.Click += (sender, args) => Environment.Exit(0);

                    continueButton.TabIndex = 0;
                    infoButton.TabIndex = 1;
                    changeFieldButton.TabIndex = 2;
                    exitButton.TabIndex = 3;

                    // Добавление кнопок на форму
                    menuForm.Controls.Add(exitButton);
                    menuForm.Controls.Add(changeFieldButton);
                    menuForm.Controls.Add(infoButton);
                    menuForm.Controls.Add(continueButton);

                    // Событие для сброса флага при закрытии формы
                    menuForm.FormClosed += (sender, args) => isMenuShown = false;

                    Application.Run(menuForm);
                }
            });

            menuThread.SetApartmentState(ApartmentState.STA);
            menuThread.Start();
        }

        // Функция для создания кастомного окна-сообщения
        private void ShowCustomMessageBox()
        {
            //Cursor.Hide();

            // Создаем новое окно для уведомления
            using (var messageBoxForm = new Form())
            {
                messageBoxForm.Text = "Сведения";
                messageBoxForm.Size = new Size(300, 530); // Размер окна уведомления
                messageBoxForm.StartPosition = FormStartPosition.Manual;
                messageBoxForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                messageBoxForm.Location = new Point(0, 330);
                messageBoxForm.MaximizeBox = false;
                messageBoxForm.MinimizeBox = false;
                messageBoxForm.ControlBox = false; // Оставляем кнопку закрытия
                messageBoxForm.TopMost = true; // Поверх всех окон
                messageBoxForm.ShowInTaskbar = false; // Убираем из панели задач                                                                                      


                // Убираем из Alt+Tab
                IntPtr handle = messageBoxForm.Handle;
                const int GWL_EXSTYLE = -20;
                const int WS_EX_TOOLWINDOW = 0x00000080;

                SetWindowLong(handle, GWL_EXSTYLE, GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);

                // Создаем TableLayoutPanel
                var tableLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 21,
                    Padding = new Padding(6),
                    AutoSize = true
                };

                // Устанавливаем равномерное распределение колонок
                //tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                //tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

                // Добавляем горячие клавиши и описания
                AddHotkeyRow(tableLayout, "Камера:", "W, A, S, D, LControl, Space");
                AddHotkeyRow(tableLayout, "Escape:", "Открыть меню");
                AddHotkeyRow(tableLayout, "Бассейн", "");
                AddHotkeyRow(tableLayout, "Q:", "Увеличить ширину");
                AddHotkeyRow(tableLayout, "E:", "Уменшить ширину");
                AddHotkeyRow(tableLayout, "F:", "Увеличить глубину");
                AddHotkeyRow(tableLayout, "R:", "Уменшить глубину");
                AddHotkeyRow(tableLayout, "Шар", "");
                AddHotkeyRow(tableLayout, "Z:", "Увеличить");
                AddHotkeyRow(tableLayout, "X:", "Уменшить");
                AddHotkeyRow(tableLayout, "Освещение", "");
                AddHotkeyRow(tableLayout, "T, Y, U:", "Увелечение по x y z");
                AddHotkeyRow(tableLayout, "alt + (T, Y, U):", "Уменьшение по x y z");
                AddHotkeyRow(tableLayout, "Цвет воды", "");
                AddHotkeyRow(tableLayout, "G, H, J:", "Увелечение по x y z");
                AddHotkeyRow(tableLayout, "alt + (G, H, J):", "Уменьшение по x y z");
                AddHotkeyRow(tableLayout, "Стрелки", "");
                AddHotkeyRow(tableLayout, "Вверх вниз:", "Выбор поля");
                AddHotkeyRow(tableLayout, "Вправо влево:", "Изменение значения");
                AddHotkeyRow(tableLayout, "Автор:", "Юшин Александр О738Б");

                messageBoxForm.Controls.Add(tableLayout);

                // Добавляем кнопку "Закрыть"
                var closeButton = new Button
                {
                    Text = "Закрыть",
                    Dock = DockStyle.Bottom
                };
                closeButton.Click += (sender, args) =>
                {
                    messageBoxForm.Close(); // Закрываем кастомное уведомление
                };

                messageBoxForm.Controls.Add(closeButton);

                // Обработка нажатия клавиши Esc для закрытия
                messageBoxForm.KeyPreview = true;
                messageBoxForm.KeyDown += (sender, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                    {
                        messageBoxForm.Close(); // Закрываем окно при нажатии Esc
                    }
                };

                // Отображаем кастомное уведомление
                messageBoxForm.ShowDialog();
            }
        }

        private void ChangeFields()
        {
            //Cursor.Hide();
            // Список всех полей и их значений
            var fields = new List<(string Name, Func<float> Getter, Action<float> Setter)>
            {
                ("HalfSizePool", () => HalfSizePool, value => HalfSizePool = value),
                ("DepthPool", () => DepthPool, value => DepthPool = value),
                ("BallSize", () => BallSize, value => BallSize = value),
                ("LightPos.X", () => LightPos.X, value => LightPos.X = value),
                ("LightPos.Y", () => LightPos.Y, value => LightPos.Y = value),
                ("LightPos.Z", () => LightPos.Z, value => LightPos.Z = value),
                ("WaterNumber.X", () => WaterNumber.X, value => WaterNumber.X = value),
                ("WaterNumber.Y", () => WaterNumber.Y, value => WaterNumber.Y = value),
                ("WaterNumber.Z", () => WaterNumber.Z, value => WaterNumber.Z = value)
            };

            using (var changeFieldsForm = new Form())
            {
                changeFieldsForm.Text = "Изменение параметров";
                changeFieldsForm.Size = new Size(260, 450);
                changeFieldsForm.StartPosition = FormStartPosition.Manual;
                changeFieldsForm.Location = new Point(0, 330);
                changeFieldsForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                changeFieldsForm.MaximizeBox = false;
                changeFieldsForm.MinimizeBox = false;
                changeFieldsForm.ControlBox = false;
                changeFieldsForm.TopMost = true; // Поверх всех окон
                changeFieldsForm.ShowInTaskbar = false; // Убираем из панели задач                                                                                      


                // Убираем из Alt+Tab
                IntPtr handle = changeFieldsForm.Handle;
                const int GWL_EXSTYLE = -20;
                const int WS_EX_TOOLWINDOW = 0x00000080;

                SetWindowLong(handle, GWL_EXSTYLE, GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);

                var panel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };

                var numericFields = new List<NumericUpDown>();
                int y = 10; // Начальная позиция по Y

                foreach (var field in fields)
                {
                    // Метка для имени поля
                    var label = new Label
                    {
                        Text = field.Name,
                        Location = new Point(10, y),
                        Width = 150,
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    panel.Controls.Add(label);

                    decimal fieldValue = (decimal)field.Getter();
                    fieldValue = Math.Round(fieldValue, 2); // Ограничиваем до 2 знаков
                    fieldValue = Math.Max(fieldValue, -100); // Минимум
                    fieldValue = Math.Min(fieldValue, 100);  // Максимум

                    // Поле для ввода значения
                    var numericUpDown = new NumericUpDown
                    {
                        Location = new Point(170, y),
                        Width = 40,
                        DecimalPlaces = 2,
                        Increment = 0.1m,
                        Minimum = -100,
                        Maximum = 100,
                        Value = fieldValue,
                        TabStop = false, // Отключаем стандартные события клавиш
                    };

                    numericUpDown.Controls[0].Visible = false;

                    // Привязка изменения значения
                    numericUpDown.ValueChanged += (sender, e) =>
                    {
                        field.Setter((float)numericUpDown.Value);
                    };

                    panel.Controls.Add(numericUpDown);
                    numericFields.Add(numericUpDown);

                    y += 40; // Шаг по Y для следующего поля
                }

                // Добавление описания внизу формы
                var infoLabel = new Label
                {
                    Text = "Нажмите Esc, чтобы закрыть окно",
                    Dock = DockStyle.Bottom,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Height = 30
                };
                changeFieldsForm.Controls.Add(infoLabel);

                changeFieldsForm.Controls.Add(panel);

                // Обработка клавиш
                changeFieldsForm.KeyPreview = true;
                changeFieldsForm.KeyDown += (sender, e) =>
                {
                    var focusedFieldIndex = numericFields.FindIndex(n => n.Focused);

                    switch (e.KeyCode)
                    {
                        case Keys.Escape:
                            changeFieldsForm.Close();
                            break;

                        case Keys.Up:
                            e.Handled = true; // Останавливаем стандартное поведение
                            if (focusedFieldIndex > 0)
                            {
                                numericFields[focusedFieldIndex - 1].Focus();
                            }
                            break;

                        case Keys.Down:
                            e.Handled = true; // Останавливаем стандартное поведение
                            if (focusedFieldIndex < numericFields.Count - 1)
                            {
                                numericFields[focusedFieldIndex + 1].Focus();
                            }
                            break;

                        case Keys.Left:
                            if (focusedFieldIndex >= 0)
                            {
                                numericFields[focusedFieldIndex].Value = Math.Max(
                                    numericFields[focusedFieldIndex].Value - 0.1m,
                                    numericFields[focusedFieldIndex].Minimum);
                            }
                            break;

                        case Keys.Right:
                            if (focusedFieldIndex >= 0)
                            {
                                numericFields[focusedFieldIndex].Value = Math.Min(
                                    numericFields[focusedFieldIndex].Value + 0.1m,
                                    numericFields[focusedFieldIndex].Maximum);
                            }
                            break;
                    }
                };


                // Добавляем кнопку "Закрыть"
                var closeButton = new Button
                {
                    Text = "Закрыть",
                    Dock = DockStyle.Bottom
                };
                closeButton.Click += (sender, args) =>
                {
                    changeFieldsForm.Close(); // Закрываем кастомное уведомление
                };

                changeFieldsForm.Controls.Add(closeButton);

                // Отображаем форму
                changeFieldsForm.ShowDialog();
            }
        }

        private void AddHotkeyRow(TableLayoutPanel table, string leftText, string rightText)
        {
            var leftLabel = new Label
            {
                Text = leftText,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Font = new Font("JetBrains", 9, FontStyle.Regular)
            };

            var rightLabel = new Label
            {
                Text = rightText,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Font = new Font("JetBrains", 9, FontStyle.Regular)
            };

            table.Controls.Add(leftLabel);
            table.Controls.Add(rightLabel);
        }

        #endregion

        #region Run

        /// <summary>
        /// Entry point of this example.
        /// </summary>
        public static void RunFragmentShader()
        {
            using (ShaderWindow example = new ShaderWindow())
            {
                // Get the title and category  of this example using reflection.
                //ExampleAttribute info = ((ExampleAttribute)example.GetType().GetCustomAttributes(false)[0]);
                //example.Title = String.Format("OpenTK | {0} {1}: {2}", info.Category, info.Difficulty, info.Title);
                example.Title = "Fragment Shader";
                example.Run(30.0, 0.0);
            }
        } 
        #region Secret
        // PInvoke для работы с окнами
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion

        /// <summary>
        /// Entry point of this example.
        /// </summary>
        public static void RunFragmentShaderWindow()
        {
            FragmentShaderSource = System.IO.File.ReadAllText("..\\..\\SWater.glsl").Split(new Char[] { '\n' });
            using (ShaderWindow example = new ShaderWindow())
            {
                // Get the title and category  of this example using reflection.
                //ExampleAttribute info = ((ExampleAttribute)example.GetType().GetCustomAttributes(false)[0]);
                //example.Title = String.Format("OpenTK | {0} {1}: {2}", info.Category, info.Difficulty, info.Title);
                example.Title = "Simple Scene";
                example.Run(30.0, 0.0);
            }
        }

        #endregion 
    }
}
