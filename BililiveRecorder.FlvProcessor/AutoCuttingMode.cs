﻿namespace BililiveRecorder.FlvProcessor
{
    public enum AutoCuttingMode : int
    {
        /// <summary>
        /// 禁用
        /// </summary>
        Disabled,
        /// <summary>
        /// 根据时间切割
        /// </summary>
        ByTime,
        /// <summary>
        /// 根据文件大小切割
        /// </summary>
        BySize,
    }
}
