CREATE TABLE `bililive_stream_crawler`.`data`
    (
        `id`             INT NOT NULL auto_increment comment '自增主键',
        `roomid`         INT NOT NULL comment '原房间号',
        `clientname`     TEXT NOT NULL comment '解析数据的client名字',
        `timestamp`      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP comment '数据入库时间',
        `roominfo`       JSON NOT NULL comment '房间信息json',
        `flvhost`        TEXT NOT NULL comment '直播流服务器域名',
        `height`         INT NOT NULL comment '视频高（FLV数据）',
        `width`          INT NOT NULL comment '视频宽（FLV数据）',
        `fps`            INT NOT NULL comment 'FPS（FLV数据）',
        `encoder`        TEXT NOT NULL comment '编码器（FLV数据）',
        `video_datarate` INT NOT NULL comment '视频码率（FLV数据）',
        `audio_datarate` INT NOT NULL comment '音频码率（FLV数据）',
        `profile`        INT NOT NULL comment 'Profile（H264）',
        `level`          INT NOT NULL comment 'Level（H264）',
        `size`           INT NOT NULL comment '十秒视频数据大小',
        `onmetadata`     JSON NOT NULL comment 'FLV Script Tag',
        `avc_dcr`        BLOB NOT NULL comment 'AVCDecoderConfigurationRecord',
        PRIMARY KEY(`id`),
        INDEX `index_roomid`(`roomid`)
    )
engine = innodb; 
