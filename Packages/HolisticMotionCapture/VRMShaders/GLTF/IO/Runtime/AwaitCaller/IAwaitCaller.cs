﻿using System;
using System.Threading.Tasks;

namespace VRMShaders
{
    /// <summary>
    /// ImporterContext の 非同期実行 LoadAsync を補助する。
    /// この関数を経由して await すること。
    /// そうしないと、同期実行 Load 時にデッドロックに陥るかもしれない。
    /// (SynchronizationContext に Post された 継続が再開されない)
    /// </summary>
    public interface IAwaitCaller
    {
        /// <summary>
        /// フレームレートを維持するために１フレーム待つ
        /// </summary>
        /// <returns></returns>
        Task NextFrame();

        /// <summary>
        /// 非同期に実行して、終了を待つ
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        Task Run(Action action);

        /// <summary>
        /// 非同期に実行して、終了を待つ
        /// </summary>
        /// <param name="action"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<T> Run<T>(Func<T> action);

        /// <summary>
        /// 指定した時間が経過している場合のみ、NextFrame() を使って1フレーム待つ
        /// </summary>
        /// <returns>タイムアウト時はNextFrame()を呼び出す。そうではない場合、Task.CompletedTaskを返す</returns>
        Task NextFrameIfTimedOut();
    }
}
