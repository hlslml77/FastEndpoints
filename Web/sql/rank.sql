-- 排行榜相关表结构与索引

-- 1) 玩家每天的运动汇总（用于审计/校验，可选）
CREATE TABLE IF NOT EXISTS `player_sport_daily` (
  `user_id` BIGINT NOT NULL,
  `date` DATE NOT NULL,
  `device_type` INT NOT NULL COMMENT '0=跑步,1=划船,2=单车,3=手环',
  `distance_meters` DECIMAL(18,2) NOT NULL DEFAULT 0,
  `calories` INT NOT NULL DEFAULT 0,
  `updated_at` DATETIME NOT NULL,
  PRIMARY KEY (`user_id`,`date`,`device_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家每日运动汇总';

-- 2) 排行榜聚合表（实时累加，查询直接 order by）
CREATE TABLE IF NOT EXISTS `pve_rank_board` (
  `period_type` INT NOT NULL COMMENT '1=周榜,2=赛季榜',
  `period_id` INT NOT NULL COMMENT '周榜=yyyyWW;赛季=yyyy',
  `device_type` INT NOT NULL COMMENT '0=跑步,1=划船,2=单车,3=手环',
  `user_id` BIGINT NOT NULL,
  `total_distance_meters` DECIMAL(18,2) NOT NULL DEFAULT 0,
  `updated_at` DATETIME NOT NULL,
  PRIMARY KEY (`period_type`,`period_id`,`device_type`,`user_id`),
  KEY `idx_rank_top` (`period_type`,`period_id`,`device_type`,`total_distance_meters` DESC, `updated_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='排行榜聚合';

-- 3) 奖励发放记录（防止重复领取）
CREATE TABLE IF NOT EXISTS `pve_rank_reward_grant` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `period_type` INT NOT NULL,
  `period_id` INT NOT NULL,
  `device_type` INT NOT NULL,
  `user_id` BIGINT NOT NULL,
  `rank` INT NOT NULL,
  `reward_json` TEXT NOT NULL,
  `created_at` DATETIME NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_period_user` (`period_type`,`period_id`,`device_type`,`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='排行榜奖励发放记录';

