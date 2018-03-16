using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectSummer.Repository.ASUDriver
{
    /// <summary>
    /// Класс для очистки блоков неуправляемой памяти
    /// </summary>
    public static class UnMemory
    {
        /// <summary>
        /// Очередь для высвобождения блоков памяти
        /// </summary>
        private static readonly Queue<IntPtr> Queue = new Queue<IntPtr>();

        public static void Enqueue(IntPtr ptr)
        {
            Queue.Enqueue(ptr);
        }

        public static void FreeIntPtr(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(ptr);
        }

        /// <summary>
        /// Освобождение блоков памяти в неуправляемом пространстве
        /// </summary>
        public static void FreeMemory()
        {
            while (Queue.Count > 0)
            {
                var temp = Queue.Dequeue();
                // освобождаем то, что записано в памяти
                Marshal.FreeCoTaskMem(temp);
            }
        }
    }

    /// <summary>
    /// Класс для работы неуправляемой памятью
    /// </summary>
    /// <typeparam name="T">Структурный тип данных</typeparam>
    public static class UnMemory<T>
      where T : struct
    {

        /// <summary>
        /// Получить указатель на структуру в неуправляемом куску памяти
        /// </summary>
        /// <param name="memoryObject">Объект для сохранения</param>
        /// <param name="ptr">Указатель</param>
        /// <typeparam name="T">Структурный тип данных</typeparam>
        public static void SaveInMem(T memoryObject, ref IntPtr ptr)
        {
            if (memoryObject.Equals(default(T)))
            {
                // объявляем указатель на кусок памяти
                ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(T)));
                UnMemory.Enqueue(ptr);
                return;
            }

            if (ptr == IntPtr.Zero)
            {
                // объявляем указатель на кусок памяти
                ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(T)));

                // записываем в память данные структуры
                Marshal.StructureToPtr(memoryObject, ptr, false);
            }
            else
            {
                // записываем в память данные структуры
                Marshal.StructureToPtr(memoryObject, ptr, true);
            }

            UnMemory.Enqueue(ptr);
        }

        /// <typeparam name="T">IntPtr, int, float</typeparam>
        /// <exception cref="System.ArgumentException">Параметр #1 должен быть массивом IntPtr, int, float</exception>
        public static void SaveInMemArr(T[] managedArray, ref IntPtr pnt, int length = 0)
        {
            Debug.Assert(managedArray != null, "Объект не должен быть Null");
            Debug.Assert(managedArray.Length != 0, "Объект не может иметь длину массива 0");

            if (length == 0)
                length = managedArray.Length;

            if (pnt == IntPtr.Zero)
            {
                // объявляем указатель на кусок памяти. Размер = размер одного элемента * количество
                //int size = Marshal.SizeOf(typeof(T)) * managedArray.Length;
                var size = Marshal.SizeOf(managedArray[0]) * managedArray.Length;
                pnt = Marshal.AllocCoTaskMem(size);
            }

            // в зависимости от типа массива, мы вызываем соответствующий метод в Marshal.Copy
            if (typeof(T) == typeof(int))
            {
                var i = managedArray as int[];
                if (i != null) Marshal.Copy(i, 0, pnt, Math.Min(i.Length, length));
            }
            else if (typeof(T) == typeof(byte))
            {
                var b = managedArray as byte[];
                if (b != null) Marshal.Copy(b, 0, pnt, Math.Min(b.Length, length));
            }
            else if (typeof(T) == typeof(float))
            {
                var f = managedArray as float[];
                if (f != null) Marshal.Copy(f, 0, pnt, Math.Min(f.Length, length));
            }
            else if (typeof(T) == typeof(char))
            {
                // читаем массив байтов и переводим в текущую кодировку
                var tArr = managedArray as char[];
                if (tArr != null)
                {
                    var b = Encoding.Default.GetBytes(tArr);
                    Marshal.Copy(b, 0, pnt, b.Length);
                }
            }
            else if (typeof(T) == typeof(IntPtr))
            {
                var p = managedArray as IntPtr[];
                if (p != null) Marshal.Copy(p, 0, pnt, Math.Min(p.Length, length));
            }
            else
                throw new ArgumentException("Параметр #1 должен быть массивом IntPtr, int, float или char");

            // запоминаем указатель, чтобы потом его почистить
            UnMemory.Enqueue(pnt);
        }

        /// <summary>
        /// Чтение структуры из неуправляемой памяти
        /// </summary>
        /// <param name="ptr">Указатель</param>
        /// <returns>Структура из памяти</returns>
        public static T ReadInMem(IntPtr ptr)
        {
            return (T)Marshal.PtrToStructure(ptr, typeof(T));
        }

        public static T[] ReadInMemArr(IntPtr ptr, int size)
        {
            if (typeof(T) == typeof(int))
            {
                var memInt = new int[size];
                Marshal.Copy(ptr, memInt, 0, size);
                return memInt as T[];
            }
            else if (typeof(T) == typeof(byte))
            {
                var memByte = new byte[size];
                Marshal.Copy(ptr, memByte, 0, size);
                return memByte as T[];
            }
            else if (typeof(T) == typeof(float))
            {
                var memFloat = new float[size];
                Marshal.Copy(ptr, memFloat, 0, size);
                return memFloat as T[];
            }
            else if (typeof(T) == typeof(IntPtr))
            {
                var memIntPtr = new IntPtr[size];
                Marshal.Copy(ptr, memIntPtr, 0, size);
                return memIntPtr as T[];
            }
            else
                throw new ArgumentException("Параметр #1 должен быть массивом int, float или char");
        }

        /// <summary>
        /// Класс переводит массивы
        /// </summary>
        public static class UnArray
        {
            /// <summary>
            /// Перевод одномерного массива в двумерный
            /// </summary>
            /// <param name="array">Исходный массив</param>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns>Двумерный массив</returns>
            public static T[,] Rank1_Rank2(T[] array, int x, int y)
            {
                var res = new T[x, y];
                var size = Buffer.ByteLength(array);
                Buffer.BlockCopy(array, 0, res, 0, size);
                return res;
            }

            /// <summary>
            /// Перевод двумерного в одномерный массив
            /// </summary>
            /// <param name="array">Исходный массив</param>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns>Одномерный массив</returns>
            public static T[] ToRank1(T[,] array, int x, int y)
            {
                var res = new T[x * y];
                var size = Buffer.ByteLength(array);
                Buffer.BlockCopy(array, 0, res, 0, size);
                return res;
            }

            /// <summary>
            /// Перевод одномерного массива в трехмерный
            /// </summary>
            /// <param name="array">Исходный массив</param>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="z"></param>
            /// <returns>Трехмерный массив</returns>
            public static T[,,] Rank1_Rank3(T[] array, int x, int y, int z)
            {
                var res = new T[x, y, z];
                var size = Buffer.ByteLength(array);
                Buffer.BlockCopy(array, 0, res, 0, size);
                return res;
            }

            /// <summary>
            /// Перевод трехмерного массива в одномерный
            /// </summary>
            /// <param name="array">Исходный массив</param>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="z"></param>
            /// <returns>Одномерный массив</returns>
            public static T[] ToRank1(T[,,] array, int x, int y, int z)
            {
                var res = new T[x * y * z];
                var size = Buffer.ByteLength(array);
                Buffer.BlockCopy(array, 0, res, 0, size);
                return res;
            }

            /// <summary>
            /// Перевод одномерного массива в четырехмерный
            /// </summary>
            /// <param name="array">Исходный массив</param>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="z"></param>
            /// <param name="w"></param>
            /// <returns>Четырехмерный массив</returns>
            public static T[,,,] Rank1_Rank4(T[] array, int x, int y, int z, int w)
            {
                var res = new T[x, y, z, w];
                var size = Buffer.ByteLength(array);
                Buffer.BlockCopy(array, 0, res, 0, size);
                return res;
            }

            /// <summary>
            /// Перевод четырехмерного массива в одномерный
            /// </summary>
            /// <param name="array">Исходный массив</param>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="z"></param>
            /// <param name="w"></param>
            /// <returns>Одномерный массив</returns>
            public static T[] ToRank1(T[,,,] array, int x, int y, int z, int w)
            {
                var res = new T[x * y * z * w];
                var size = Buffer.ByteLength(array);
                Buffer.BlockCopy(array, 0, res, 0, size);
                return res;
            }
        }
    }
}
