using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Billiards.Core
{
    public enum GameLanguage
    {
        Vietnamese = 0,
        English = 1,
        French = 2,
        Spanish = 3,
        Japanese = 4,
        Korean = 5,
        Chinese = 6
    }

    /// <summary>
    /// Hệ thống quản lý đa ngôn ngữ (Localization) tập trung cho toàn bộ Game Bida 3D.
    /// Hỗ trợ chuyển đổi thời gian thực cho cả Sảnh Chờ (Lobby) và Trận Đấu (Gameplay HUD, Foul, Result, Stats).
    /// </summary>
    public static class LocalizationManager
    {
        public const string PrefsKey = "GameLanguage";
        public const string PrefsIndexKey = "LanguageIndex";

        public static event Action OnLanguageChanged;

        private static GameLanguage currentLanguage = GameLanguage.Vietnamese;

        public static readonly string[] LanguageNames = new string[]
        {
            "Tiếng Việt (VI)",
            "English (EN)",
            "Français (FR)",
            "Español (ES)",
            "日本語 (JA)",
            "한국어 (KO)",
            "中文 (ZH)"
        };

        public static GameLanguage CurrentLanguage => currentLanguage;
        public static int CurrentLanguageIndex => (int)currentLanguage;

        private static readonly Dictionary<string, string[]> Dict = new Dictionary<string, string[]>
        {
            // ===== LOBBY: MAIN MENU =====
            { "menu_play_ai", new[] { "CHƠI VỚI AI", "PLAY VS AI", "JOUER VS IA", "JUGAR VS IA", "AI対戦", "AI 대전", "人机对战" } },
            { "menu_play_online", new[] { "ĐẤU ONLINE", "ONLINE PVP", "JOUEURS EN LIGNE", "JUGAR EN LÍNEA", "オンライン対戦", "온라인 대전", "在线对战" } },
            { "menu_rules", new[] { "LUẬT CHƠI", "RULES", "RÈGLES", "REGLAS", "ルール", "규칙", "规则说明" } },
            { "menu_settings", new[] { "CÀI ĐẶT", "SETTINGS", "PARAMÈTRES", "AJUSTES", "設定", "설정", "游戏设置" } },
            { "menu_quit", new[] { "THOÁT", "QUIT", "QUITTER", "SALIR", "終了", "종료", "退出游戏" } },

            // ===== LOBBY: SETTINGS MODAL =====
            { "settings_title", new[] { "CÀI ĐẶT", "SETTINGS", "PARAMÈTRES", "AJUSTES", "設定", "설정", "设置" } },
            { "settings_master", new[] { "ÂM LƯỢNG TỔNG", "MASTER VOLUME", "VOLUME PRINCIPAL", "VOLUMEN PRINCIPAL", "マスター音量", "마스터 볼륨", "主音量" } },
            { "settings_music", new[] { "ÂM LƯỢNG NHẠC", "MUSIC VOLUME", "MUSIQUE", "MÚSICA", "BGM音量", "배경음악", "音乐音量" } },
            { "settings_sfx", new[] { "HIỆU ỨNG ÂM THANH", "SOUND EFFECTS", "EFFETS SONORES", "EFECTOS DE SONIDO", "SE音量", "효과음", "音效音量" } },
            { "settings_language", new[] { "NGÔN NGỮ (LANGUAGE)", "LANGUAGE (NGÔN NGỮ)", "LANGUE (LANGUAGE)", "IDIOMA (LANGUAGE)", "言語 (LANGUAGE)", "언어 (LANGUAGE)", "语言 (LANGUAGE)" } },
            { "settings_close", new[] { "ĐÓNG", "CLOSE", "FERMER", "CERRAR", "閉じる", "닫기", "关闭" } },

            // ===== LOBBY: DIFFICULTY / PRE-MATCH =====
            { "diff_title", new[] { "CẤU HÌNH TRẬN ĐẤU VỚI AI", "AI MATCH CONFIGURATION", "CONFIGURATION DU MATCH IA", "CONFIGURACIÓN PARTIDA IA", "AI対戦マッチ設定", "AI 매치 설정", "人机对战配置" } },
            { "diff_easy", new[] { "DỄ", "EASY", "FACILE", "FÁCIL", "かんたん", "쉬움", "简单" } },
            { "diff_normal", new[] { "VỪA", "NORMAL", "NORMAL", "NORMAL", "ふつう", "보통", "普通" } },
            { "diff_hard", new[] { "KHÓ", "HARD", "DIFFICILE", "DIFÍCIL", "むずかしい", "어려움", "困难" } },
            { "diff_race", new[] { "THỂ THỨC CHẠM", "RACE TO TARGET", "OBJECTIF DE MANCHES", "OBJETIVO DE PARTIDAS", "マッチ形式", "승리 목표", "比赛局数" } },
            { "diff_timer", new[] { "THỜI GIAN CÚ ĐÁNH", "SHOT TIMER", "TEMPS PAR COUP", "TIEMPO POR TIRO", "ショット制限時間", "샷 제한시간", "击球限时" } },
            { "diff_player_name", new[] { "TÊN CƠ THỦ", "PLAYER NAME", "NOM DU JOUEUR", "NOMBRE DEL JUGADOR", "プレイヤー名", "플레이어 이름", "玩家名称" } },
            { "diff_aim_guide", new[] { "BẬT ĐƯỜNG NGẮM TIA BI CÁI", "ENABLE AIM GUIDE LINE", "AFFICHER LE GUIDE DE VISÉE", "ACTIVAR GUÍA DE TIRO", "エイムガイド表示", "조준선 가이드 켜기", "开启瞄准辅助线" } },
            { "diff_rack_intro", new[] { "HOẠT ẢNH XẾP BI ĐẦU TRẬN", "RACK INTRO ANIMATION", "ANIMATION DE CASSE", "ANIMACIÓN DE APERTURA", "ラック配置演出", "랙 인트로 연출", "开局摆球动画" } },
            { "diff_start", new[] { "BẮT ĐẦU TRẬN ĐẤU", "START MATCH", "COMMENCER LE MATCH", "INICIAR PARTIDA", "試合開始", "매치 시작", "开始对战" } },

            // ===== LOBBY: QUIT CONFIRM =====
            { "quit_title", new[] { "XÁC NHẬN THOÁT", "QUIT CONFIRMATION", "QUITTER LE JEU", "CONFIRMAR SALIDA", "ゲーム終了確認", "게임 종료 확인", "退出确认" } },
            { "quit_msg", new[] { "Bạn có chắc chắn muốn thoát khỏi trò chơi?", "Are you sure you want to quit the game?", "Voulez-vous vraiment quitter le jeu ?", "¿Estás seguro de que deseas salir del juego?", "ゲームを終了してもよろしいですか？", "게임을 종료하시겠습니까?", "确定要退出游戏吗？" } },
            { "btn_yes", new[] { "CÓ", "YES", "OUI", "SÍ", "はい", "예", "确定" } },
            { "btn_no", new[] { "KHÔNG", "NO", "NON", "NO", "いいえ", "아니오", "取消" } },

            // ===== LOBBY: RULES =====
            { "rules_title", new[] { "LUẬT THI ĐẤU WPA 8-BALL", "WPA 8-BALL RULES", "RÈGLES DU 8-BALL", "REGLAS WPA 8-BALL", "WPA公式8ボール規則", "WPA 8볼 공식 규정", "WPA 8球官方规则" } },
            { "rules_tab_overview", new[] { "TỔNG QUAN", "OVERVIEW", "GÉNÉRAL", "GENERAL", "概要", "개요", "概述" } },
            { "rules_tab_break", new[] { "KHAI CUỘC", "BREAK SHOT", "CASSE", "SAQUE", "ブレイク", "브레이크", "开球" } },
            { "rules_tab_fouls", new[] { "PHẠM QUY", "FOULS", "FAUTES", "FALTAS", "ファウル", "파울", "犯规" } },
            { "rules_tab_8ball", new[] { "BI SỐ 8", "8-BALL", "BILLE 8", "BOLA 8", "8番ボール", "8번 공", "8号黑球" } },
            { "rules_tab_loss", new[] { "THUA VÁN", "LOSS OF RACK", "DÉFAITE", "PÉRDIDA", "敗戦条件", "패배 조건", "判负" } },

            // ===== GAMEPLAY: BALL GROUPS =====
            { "group_open", new[] { "BÀN MỞ (OPEN)", "OPEN TABLE", "TABLE OUVERTE", "MESA ABIERTA", "オープンテーブル", "오픈 테이블", "开放球局" } },
            { "group_solids", new[] { "BI TRƠN (SOLIDS 1-7)", "SOLIDS (1-7)", "PLEINES (1-7)", "LISAS (1-7)", "ソリッド (1-7)", "단색구 (1-7)", "纯色球 (1-7)" } },
            { "group_stripes", new[] { "BI SỌC (STRIPES 9-15)", "STRIPES (9-15)", "RAYÉES (9-15)", "RAYADAS (9-15)", "ストライプ (9-15)", "줄무늬구 (9-15)", "花色球 (9-15)" } },
            { "group_eight_ball", new[] { "BI SỐ 8 (EIGHT BALL)", "8-BALL", "BILLE 8", "BOLA 8", "8番ボール", "8번 공", "8号黑球" } },

            // ===== GAMEPLAY: HUD BUTTONS =====
            { "hud_call_shot", new[] { "CHỈ ĐỊNH LỖ", "CALL SHOT", "DÉSIGNER POCHE", "CANTAR TRONERA", "ポケットコール", "포켓 지정", "指定球袋" } },
            { "hud_safety", new[] { "ĐÁNH AN TOÀN", "SAFETY", "SÉCURITÉ", "SEGURIDAD", "セーフティ", "세이프티", "安全球" } },
            { "hud_ball_in_hand", new[] { "ĐẶT BI TỰ DO", "BALL IN HAND", "BILLE EN MAIN", "BOLA EN MANO", "フリーボール", "프리볼", "手中球" } },
            { "hud_stats", new[] { "THỐNG KÊ", "STATS", "STATISTIQUES", "ESTADÍSTICAS", "スタッツ", "통계", "比赛统计" } },
            { "hud_settings", new[] { "CÀI ĐẶT", "SETTINGS", "PARAMÈTRES", "AJUSTES", "設定", "설정", "设置" } },
            { "hud_rules", new[] { "LUẬT", "RULES", "RÈGLES", "REGLAS", "ルール", "규칙", "规则" } },
            { "hud_help", new[] { "HƯỚNG DẪN", "HELP", "AIDE", "AYUDA", "ヘルプ", "도움말", "帮助" } },
            { "hud_surrender", new[] { "ĐẦU HÀNG", "SURRENDER", "ABANDONNER", "RENDIRSE", "降参", "기권", "认输投降" } },
            { "hud_quit_match", new[] { "THOÁT TRẬN", "QUIT MATCH", "QUITTER LE MATCH", "SALIR DE PARTIDA", "試合終了", "매치 나가기", "退出比赛" } },
            { "hud_resume", new[] { "TIẾP TỤC", "RESUME", "REPRENDRE", "CONTINUAR", "再開", "계속하기", "继续对局" } },
            { "hud_next_rack", new[] { "VÁN TIẾP THEO", "NEXT RACK", "MANCHE SUIVANTE", "SIGUIENTE PARTIDA", "次のラック", "다음 랙", "下一局" } },
            { "hud_rematch", new[] { "ĐẤU LẠI", "REMATCH", "REVANCHE", "REVANCHA", "再戦", "재경기", "再来一局" } },
            { "hud_return_lobby", new[] { "VỀ SẢNH CHỜ", "RETURN TO LOBBY", "RETOUR AU MENU", "VOLVER AL MENÚ", "ロビーに戻る", "로비로 이동", "返回大厅" } },

            // ===== GAMEPLAY: MATCH START BANNER =====
            { "match_start_title", new[] { "THI ĐẤU WPA 8-BALL", "WPA 8-BALL MATCH", "MATCH WPA 8-BALL", "PARTIDA WPA 8-BALL", "WPA 8ボール対戦", "WPA 8볼 매치", "WPA 8球国际赛事" } },
            { "match_start_format", new[] { "THỂ THỨC: RACE TO {0} (CHẠM {0})", "FORMAT: RACE TO {0}", "FORMAT : PREMIER À {0}", "FORMATO: AL PRIMERO DE {0}", "形式: {0}ラック先取", "방식: {0}점 선승", "赛制：抢 {0} 局" } },
            { "match_start_rules", new[] { "LUẬT THI ĐẤU: WPA 8-BALL QUỐC TẾ", "RULES: WPA 8-BALL INTERNATIONAL", "RÈGLES : WPA 8-BALL INTERNATIONAL", "REGLAS: WPA 8-BALL OFICIALES", "規則: WPA国際公式8ボール", "규칙: WPA 국제 공식 8볼", "规则：WPA国际8球官方规则" } },
            { "match_start_breaker", new[] { "QUYỀN KHAI CUỘC: <b>{0}</b>", "BREAK SHOT: <b>{0}</b>", "CASSE INITIALE : <b>{0}</b>", "SAQUE INICIAL: <b>{0}</b>", "ブレイク権: <b>{0}</b>", "선공 브레이크: <b>{0}</b>", "开球选手：<b>{0}</b>" } },
            { "match_start_btn", new[] { "BẮT ĐẦU!", "START!", "C'EST PARTI !", "¡EMPEZAR!", "スタート！", "시작!", "开始！" } },

            // ===== GAMEPLAY: TURNS & TOASTS =====
            { "turn_your_turn", new[] { "LƯỢT CỦA BẠN", "YOUR TURN", "À VOTRE TOUR", "TU TURNO", "あなたのターン", "당신의 턴", "您的回合" } },
            { "turn_opponent", new[] { "LƯỢT CỦA ĐỐI THỦ", "OPPONENT'S TURN", "TOUR ADVERSE", "TURNO DEL OPONENTE", "相手のターン", "상대방 턴", "对手回合" } },
            { "turn_new_turn", new[] { "LƯỢT ĐÁNH MỚI", "NEW TURN", "NOUVEAU TOUR", "NUEVO TURNO", "新しいターン", "새 턴", "新回合" } },
            { "turn_playing_now", new[] { "Đến lượt thi đấu của: {0}", "Now shooting: {0}", "Au tour de : {0}", "Turno de: {0}", "{0} のショット", "{0} 님의 차례", "当前击球选手：{0}" } },
            { "turn_group_assigned", new[] { "{0} đã chọn nhóm: {1}", "{0} has chosen: {1}", "{0} a choisi : {1}", "{0} ha elegido: {1}", "{0} が {1} を選択", "{0} 님이 {1} 선택", "{0} 确定球组：{1}" } },
            { "turn_ball_in_hand", new[] { "BI TRONG TAY", "BALL IN HAND", "BILLE EN MAIN", "BOLA EN MANO", "フリーボール", "프리볼", "手中球" } },
            { "turn_bih_desc", new[] { "Nhấp chuột trái lên bàn để đặt bi cái", "Left-click table to place cue ball", "Clic gauche sur la table pour poser la bille", "Clic izquierdo en la mesa para colocar bola", "テーブルをクリックして手玉を配置", "테이블을 클릭하여 큐볼을 배치하세요", "左键点击台面摆放母球" } },
            { "turn_banner_your_turn", new[] { "ĐẾN LƯỢT BẠN", "YOUR TURN", "À VOTRE TOUR", "TU TURNO", "あなたのターン", "당신의 턴", "轮到您击球" } },
            { "turn_banner_your_turn_sub", new[] { "Hãy nhắm và thực hiện cú đánh", "Aim and execute your shot", "Visez et tirez", "Apunta y tira", "狙いを定めてショット", "조준하고 샷을 진행하세요", "请瞄准并击球" } },
            { "turn_banner_opponent", new[] { "ĐẾN LƯỢT ĐỐI THỦ", "OPPONENT'S TURN", "TOUR ADVERSE", "TURNO DEL OPONENTE", "相手のターン", "상대방 턴", "对手回合" } },
            { "turn_banner_opponent_sub", new[] { "{0} đang vào thế đánh...", "{0} is taking their turn...", "{0} joue son tour...", "{0} está jugando...", "{0} がショット準備中...", "{0} 님이 샷 준비 중...", "{0} 正在准备击球..." } },
            { "turn_banner_opponent_sub_simple", new[] { "LƯỢT CỦA {0}", "{0}'S TURN", "TOUR DE {0}", "TURNO DE {0}", "{0} のターン", "{0} 님의 턴", "{0} 的回合" } },
            { "break_ball_pocketed_title", new[] { "ĐÃ CÓ BI VÀO LỖ", "OBJECT BALL POCKETED", "BILLE EMPOCHÉE", "¡BOLA EMBOCADA!", "的球ポケットイン！", "공이 포켓에 들어감!", "目标球已落袋" } },
            { "break_badge", new[] { "PHÁ BI", "BREAK", "CASSE", "SAQUE", "ブレイク", "브레이크", "开球" } },

            // ===== GAMEPLAY: FOULS =====
            { "foul_badge", new[] { "LỖI", "FOUL", "FAUTE", "FALTA", "ファウル", "파울", "犯规" } },
            { "foul_sub", new[] { "ĐỐI THỦ ĐƯỢC ĐẶT BI CÁI TỰ DO", "OPPONENT RECEIVES BALL IN HAND", "BILLE EN MAIN POUR L'ADVERSAIRE", "BOLA EN MANO PARA EL OPONENTE", "相手プレイヤーにフリーボール", "상대방 프리볼 획득", "对手获得手中球" } },
            { "foul_scratch", new[] { "BI CÁI RƠI LỖ", "CUE BALL SCRATCH", "BILLE BLANCHE EMPOCHÉE", "BOLA BLANCA ENTRADA", "スクラッチ（手玉落ち）", "스크래치 (큐볼 포켓)", "母球落袋犯规" } },
            { "foul_wrong_ball", new[] { "ĐÁNH SAI BI ĐẦU TIÊN", "WRONG BALL FIRST", "MAUVAISE BILLE TOUCHÉE", "BOLA INCORRECTA", "ファーストヒットミス", "오구 타격 파울", "错击目标球犯规" } },
            { "foul_no_ball_hit", new[] { "KHÔNG CHẠM BI NÀO", "NO BALL HIT", "AUCUNE BILLE TOUCHÉE", "NINGUNA BOLA TOCADA", "ヒットなしファウル", "타격 없음 파울", "未击中任何球犯规" } },
            { "foul_no_rail", new[] { "KHÔNG BI NÀO CHẠM BĂNG", "NO RAIL AFTER CONTACT", "AUCUNE BILLE À LA BANDE", "NINGUNA BOLA TOCÓ BANDA", "クッション不達ファウル", "쿠션 미도달 파울", "碰库不足犯规" } },
            { "foul_illegal_break", new[] { "KHAI CUỘC KHÔNG HỢP LỆ", "ILLEGAL BREAK", "CASSE NON CONFORME", "SAQUE ILEGAL", "無効ブレイク", "무효 브레이크", "无效开球" } },
            { "foul_off_table", new[] { "BI VĂNG KHỎI BÀN", "BALL DRIVEN OFF TABLE", "BILLE ÉJECTÉE DE LA TABLE", "BOLA FUERA DE MESA", "ボール場外", "공 장외 이탈", "球飞出球台" } },
            { "foul_timeout", new[] { "HẾT GIỜ", "SHOT CLOCK EXPIRED", "TEMPS ÉCOULÉ", "TIEMPO AGOTADO", "タイムオーバー", "시간 초과", "击球超时" } },
            { "foul_bad_placement", new[] { "ĐẶT BI CÁI SAI VỊ TRÍ", "BAD CUE BALL PLACEMENT", "MAUVAIS EMPLACEMENT", "COLOCACIÓN INCORRECTA", "手玉の不正配置", "큐볼 배치 오류", "母球摆位错误" } },
            { "foul_out_of_turn", new[] { "ĐÁNH SAI LƯỢT", "PLAYING OUT OF TURN", "TIR HORS DU TOUR", "TIRO FUERA DE TURNO", "順番外ショット", "순서 위반", "非选手顺序击球" } },
            { "foul_balls_moving", new[] { "ĐÁNH KHI BI CÒN LĂN", "BALLS STILL MOVING", "BILLES ENCORE EN MOUVEMENT", "BOLAS EN MOVIMIENTO", "静止前のショット", "정지 전 타격", "台面运动中击球" } },
            { "foul_double_hit", new[] { "ĐÁNH HAI LẦN", "DOUBLE HIT", "DOUBLE CONTACT", "DOBLE GOLPE", "二度撞き", "더블 히트 (투터치)", "连击犯规" } },
            { "foul_push_shot", new[] { "ĐẨY BI", "PUSH SHOT", "POUSSETTE", "EMPUJE", "プッシュショット", "밀어치기", "推杆犯规" } },
            { "foul_generic", new[] { "PHẠM QUY", "FOUL", "FAUTE", "FALTA", "ファウル", "파울", "犯规" } },

            // ===== GAMEPLAY: WIN & RESULT =====
            { "win_badge", new[] { "THẮNG VÁN", "RACK WIN", "MANCHE GAGNÉE", "PARTIDA GANADA", "ラック獲得", "랙 승리", "本局获胜" } },
            { "lose_badge", new[] { "THUA VÁN", "RACK LOST", "MANCHE PERDUE", "PARTIDA PERDIDA", "ラック敗北", "랙 패배", "本局失利" } },
            { "rack_you_won", new[] { "BẠN THẮNG VÁN NÀY!", "YOU WON THIS RACK!", "VOUS AVEZ GAGNÉ CETTE MANCHE !", "¡GANASTE ESTA PARTIDA!", "このラックに勝利しました！", "이번 랙을 승리했습니다!", "你赢得了本局！" } },
            { "rack_you_lost", new[] { "BẠN THUA VÁN NÀY :((", "YOU LOST THIS RACK :((", "VOUS AVEZ PERDU CETTE MANCHE :((", "PERDISTE ESTA PARTIDA :((", "このラックを落としました :((", "이번 랙을 패배했습니다 :((", "本局惜败 :((" } },
            { "score_subtitle", new[] { "TỈ SỐ: {0} - {1}", "SCORE: {0} - {1}", "SCORE : {0} - {1}", "MARCADOR: {0} - {1}", "スコア: {0} - {1}", "스코어: {0} - {1}", "比分：{0} - {1}" } },
            { "win_sub", new[] { "VÁN TIẾP THEO SẮP BẮT ĐẦU", "NEXT RACK STARTING SOON", "PROCHAINE MANCHE DANS UN INSTANT", "SIGUIENTE PARTIDA PRONTO", "次のラックがまもなく始まります", "다음 랙이 곧 시작됩니다", "下一局即将开始" } },
            { "result_rack_end_title", new[] { "KẾT THÚC VÁN ĐẤU (RACK END)", "RACK COMPLETED", "MANCHE TERMINÉE", "PARTIDA FINALIZADA", "ラック終了", "랙 종료", "本局结束" } },
            { "result_rack_win_text", new[] { "{0} CHIẾN THẮNG VÁN!", "{0} WINS THE RACK!", "{0} REMPORTE LA MANCHE !", "¡{0} GANA LA PARTIDA!", "{0} がラックを獲得！", "{0} 님이 랙에서 승리했습니다!", "{0} 赢得本局！" } },
            { "result_current_score", new[] { "TỈ SỐ HIỆN TẠI: {0} - {1} (Race to {2})", "CURRENT SCORE: {0} - {1} (Race to {2})", "SCORE ACTUEL : {0} - {1} (Premier à {2})", "MARCADOR: {0} - {1} (Al {2})", "現在のスコア: {0} - {1} ({2}ラック先取)", "현재 스코어: {0} - {1} ({2}선승)", "当前比分：{0} - {1}（抢 {2}）" } },
            { "result_next_rack_desc", new[] { "Nhấn Ván Tiếp Theo để tiếp tục xếp bi và thi đấu ván mới.", "Press Next Rack to re-rack and continue the match.", "Appuyez sur Manche Suivante pour continuer.", "Presiona Siguiente Partida para continuar.", "次のラックボタンを押して試合を続行します。", "다음 랙을 눌러 경기를 계속하세요.", "点击下一局继续比赛。" } },
            { "result_match_end_title", new[] { "TRẬN ĐẤU KẾT THÚC (MATCH OVER)", "MATCH FINISHED", "MATCH TERMINÉ", "FIN DEL ENCUENTRO", "試合終了", "매치 종료", "比赛结束" } },
            { "result_match_winner", new[] { "🏆 {0} CHIẾN THẮNG CHUNG CUỘC! 🏆", "🏆 {0} WINS THE MATCH! 🏆", "🏆 {0} EST LE GRAND VAINQUEUR ! 🏆", "🏆 ¡{0} GANA EL ENCUENTRO! 🏆", "🏆 {0} が総合優勝！ 🏆", "🏆 {0} 님이 최종 우승했습니다! 🏆", "🏆 {0} 获得总冠军！ 🏆" } },
            { "result_final_score", new[] { "TỈ SỐ CHUNG CUỘC: {0} - {1}", "FINAL SCORE: {0} - {1}", "SCORE FINAL : {0} - {1}", "MARCADOR FINAL: {0} - {1}", "最終スコア: {0} - {1}", "최종 스코어: {0} - {1}", "最终总比分：{0} - {1}" } },
            { "result_congrats", new[] { "Chúc mừng nhà vô địch đã đạt đủ số ván thắng theo thể thức Race-to!", "Congratulations to the champion for reaching the race target!", "Félicitations au champion ayant atteint l'objectif !", "¡Felicitaciones al campeón por alcanzar la meta!", "規定ラック数に到達し、見事栄冠に輝きました！", "목표 승수를 달성한 우승자에게 축하를 전합니다!", "恭喜冠军达到局数目标夺得桂冠！" } },

            // ===== GAMEPLAY: CONFIRM DIALOGS =====
            { "confirm_surrender_title", new[] { "XÁC NHẬN ĐẦU HÀNG", "CONFIRM SURRENDER", "CONFIRMER L'ABANDON", "CONFIRMAR RENDICIÓN", "降参の確認", "기권 확인", "确认投降" } },
            { "confirm_quit_title", new[] { "XÁC NHẬN THOÁT TRẬN", "CONFIRM QUIT MATCH", "QUITTER LE MATCH", "CONFIRMAR SALIDA", "試合終了の確認", "매치 종료 확인", "确认退出比赛" } },
            { "confirm_surrender_msg", new[] { "Bạn có chắc chắn muốn ĐẦU HÀNG ván đấu này không?", "Are you sure you want to SURRENDER this rack?", "Voulez-vous vraiment ABANDONNER cette manche ?", "¿Estás seguro de que deseas RENDIRTE en esta partida?", "このラックを降参してもよろしいですか？", "이번 랙을 정말 기권하시겠습니까?", "确定要在本局认输吗？" } },
            { "confirm_quit_match_msg", new[] { "Bạn có chắc chắn muốn THOÁT trận đấu về màn hình chính không?", "Are you sure you want to QUIT and return to main menu?", "Voulez-vous vraiment QUITTER le match ?", "¿Estás seguro de que deseas SALIR al menú principal?", "メインメニューに戻ってもよろしいですか？", "매치를 종료하고 메인 화면으로 나가시겠습니까?", "确定要退出比赛返回主界面吗？" } },

            // ===== GAMEPLAY: STATS PANEL METRICS =====
            { "stat_overview", new[] { "TỔNG QUAN", "OVERVIEW", "VUE D'ENSEMBLE", "RESUMEN", "概要", "개요", "综合概览" } },
            { "stat_shots", new[] { "Số cú", "Shots Taken", "Coups joués", "Tiros realizados", "総ショット数", "총 샷 수", "总出杆数" } },
            { "stat_visits", new[] { "Lượt tới bàn", "Table Visits", "Visites à la table", "Visitas a mesa", "テーブル登壇回数", "테이블 진입", "上台轮次" } },
            { "stat_balls_pocketed", new[] { "Bi vào lỗ", "Balls Pocketed", "Billes empochées", "Bolas embocadas", "ポケット数", "포켓 성공 공", "进球总数" } },
            { "stat_longest_run", new[] { "Chuỗi bi dài nhất", "Longest Run", "Meilleure série", "Mayor racha", "連続ポケット記録", "최다 연속 포켓", "最高连续进球" } },
            { "stat_accuracy", new[] { "ĐỘ CHÍNH XÁC", "ACCURACY", "PRÉCISION", "PRECISIÓN", "精度", "정확도", "精准统计" } },
            { "stat_pot_rate", new[] { "Tỉ lệ vào lỗ", "Potting Rate", "Taux d'empochage", "Efectividad de tiro", "ポット成功率", "포켓 성공률", "进球率" } },
            { "stat_called_rate", new[] { "Gọi bi trúng", "Called Shot Rate", "Tirs annoncés réussis", "Acierto de cantadas", "コールショット成功率", "콜샷 성공률", "指袋成功率" } },
            { "stat_bih_converted", new[] { "Đặt bi tự do → vào bi", "Ball-in-Hand Conversion", "Bille en main réussie", "Conversión bola en mano", "フリーボール得点率", "프리볼 득점 전환", "自由球进球转化" } },
            { "stat_run_outs", new[] { "Dọn bàn", "Run Outs", "Fermetures de table", "Limpiezas de mesa", "ランアウト（取りきり）", "런아웃 (완봉)", "清台" } },
            { "stat_break", new[] { "KHAI CUỘC", "BREAK", "CASSE", "SAQUE", "ブレイク", "브레이크", "开球统计" } },
            { "stat_legal_breaks", new[] { "Khai cuộc hợp lệ", "Legal Breaks", "Casses valides", "Saques legales", "有効ブレイク率", "합법 브레이크", "合法开球率" } },
            { "stat_break_pocketed", new[] { "Bi vào khi khai cuộc", "Break Pocketed", "Billes à la casse", "Embocadas en saque", "ブレイクイン", "브레이크 포켓 공", "开球进球数" } },
            { "stat_break_and_runs", new[] { "Khai cuộc & dọn bàn", "Break and Runs", "Casse et fermeture", "Saque y limpieza", "ブレイク＆ランアウト", "브레이크 앤 런아웃", "黄金开球清台" } },
            { "stat_fouls", new[] { "LỖI", "FOULS", "FAUTES", "FALTAS", "ファウル", "파울", "犯规数据" } },
            { "stat_total_fouls", new[] { "Tổng lỗi", "Total Fouls", "Total fautes", "Total faltas", "総ファウル数", "총 파울 수", "犯规总数" } },
            { "stat_foul_rate", new[] { "Tỉ lệ lỗi", "Foul Rate", "Taux de fautes", "Tasa de faltas", "ファウル率", "파울율", "犯规率" } },
            { "stat_scratch_stat", new[] { "Bi cái vào lỗ", "Cue Ball Scratches", "Billes blanches tombées", "Blancas embocadas", "スクラッチ回数", "큐볼 포켓 횟수", "母球落袋" } },
            { "stat_wrong_ball_stat", new[] { "Chạm sai bi", "Wrong Ball Hits", "Mauvaises billes", "Bolas incorrectas", "ファーストヒットミス", "오구 타격", "首碰错球" } },
            { "stat_no_rail_stat", new[] { "Không chạm băng", "No Rail Fouls", "Bande non touchée", "Sin banda", "クッション不達", "쿠션 미도달", "未碰库" } },
            { "stat_timeout_stat", new[] { "Hết giờ", "Timeouts", "Temps dépassé", "Tiempos agotados", "タイムオーバー", "시간 초과", "超时" } },
            { "stat_other_fouls", new[] { "Lỗi khác", "Other Fouls", "Autres fautes", "Otras faltas", "その他のファウル", "기타 파울", "其他犯规" } },
            { "stat_tactics", new[] { "CHIẾN THUẬT & KỸ THUẬT", "TACTICS & SKILLS", "TACTIQUE & TECHNIQUE", "TÁCTICA Y TÉCNICA", "戦術と技術", "전술 및 기술", "战术与技术" } },
            { "stat_safeties", new[] { "Phòng thủ thành công", "Safeties Succeeded", "Sécurités réussies", "Defensas con éxito", "セーフティ成功率", "세이프티 성공률", "防守成功率" } },
            { "stat_avg_power", new[] { "Lực đánh trung bình", "Avg Shot Power", "Puissance moyenne", "Fuerza promedio", "平均ショットパワー", "평균 샷 파워", "平均击球力度" } },
            { "stat_spin_shots", new[] { "Cú có xoay bi", "Spin Shots", "Coups avec effet", "Tiros con efecto", "スピンショット数", "스핀 샷 수", "加塞旋转球" } },
            { "stat_avg_think", new[] { "Thời gian nghĩ TB", "Avg Think Time", "Temps de réflexion", "Tiempo promedio", "平均思考時間", "평균 고민 시간", "平均思考时长" } },

            // ===== GAMEPLAY: POCKET NAMES =====
            { "pocket_0", new[] { "Lỗ Góc Trên Trái", "Top-Left Corner", "Poche Haut-Gauche", "Esquina Superior Izq.", "左上コーナー", "좌측 상단 코너", "左上底袋" } },
            { "pocket_1", new[] { "Lỗ Góc Dưới Trái", "Bottom-Left Corner", "Poche Bas-Gauche", "Esquina Inferior Izq.", "左下コーナー", "좌측 하단 코너", "左下底袋" } },
            { "pocket_2", new[] { "Lỗ Góc Trên Phải", "Top-Right Corner", "Poche Haut-Droite", "Esquina Superior Der.", "右上コーナー", "우측 상단 코너", "右上底袋" } },
            { "pocket_3", new[] { "Lỗ Góc Dưới Phải", "Bottom-Right Corner", "Poche Bas-Droite", "Esquina Inferior Der.", "右下コーナー", "우측 하단 코너", "右下底袋" } },
            { "pocket_4", new[] { "Lỗ Giữa Trái", "Middle-Left Side", "Poche Milieu-Gauche", "Lateral Izquierda", "左サイド", "좌측 사이드", "左中腰袋" } },
            { "pocket_5", new[] { "Lỗ Giữa Phải", "Middle-Right Side", "Poche Milieu-Droite", "Lateral Derecha", "右サイド", "우측 사이드", "右中腰袋" } },

            // ===== RULES MODAL: TABS & BUTTONS =====
            { "rules_tab_1_title", new[] { "1. QUY TẮC KHAI CUỘC (BREAK SHOT)", "1. BREAK SHOT RULES", "1. RÈGLES DE LA CASSE", "1. REGLAS DEL SAQUE", "1. ブレイクショット規則", "1. 브레이크 샷 규칙", "1. 开球规则" } },
            { "rules_tab_2_title", new[] { "2. BÀN MỞ & CHỌN NHÓM (OPEN TABLE & GROUPS)", "2. OPEN TABLE & GROUPS", "2. TABLE OUVERTE & GROUPES", "2. MESA ABIERTA Y GRUPOS", "2. オープンテーブルとグループ選択", "2. 오픈 테이블 및 그룹 선택", "2. 开放球局与分组" } },
            { "rules_tab_3_title", new[] { "3. THỨ TỰ TIẾP XÚC, CHẠM BĂNG & CÁC LỖI PHẠT (FOULS)", "3. FOULS & PENALTIES", "3. FAUTES & PÉNALITÉS", "3. FALTAS Y PENALIZACIONES", "3. ファウルと反則", "3. 파울 및 페널티", "3. 犯规与判罚" } },
            { "rules_tab_4_title", new[] { "4. QUY TẮC VỀ BI SỐ 8 (THE 8-BALL)", "4. 8-BALL RULES", "4. RÈGLES DE LA BILLE 8", "4. REGLAS DE LA BOLA 8", "4. 8番ボール（エイトボール）規則", "4. 8번 공 규칙", "4. 8号球规则" } },
            { "rules_tab_5_title", new[] { "5. ĐIỀU KIỆN THẮNG TRẬN (WIN CONDITIONS)", "5. WIN CONDITIONS", "5. CONDITIONS DE VICTOIRE", "5. CONDICIONES DE VICTORIA", "5. 勝利条件", "5. 승리 조건", "5. 胜负判定" } },
            { "rules_tab_1_btn", new[] { "Khai Cuộc", "Break Shot", "Casse", "Saque", "ブレイク", "브레이크", "开球" } },
            { "rules_tab_2_btn", new[] { "Bàn Mở", "Open Table", "Table Ouverte", "Mesa Abierta", "オープン", "오픈 테이블", "开放局" } },
            { "rules_tab_3_btn", new[] { "Phạm Quy", "Fouls", "Fautes", "Faltas", "ファウル", "파울", "犯规" } },
            { "rules_tab_4_btn", new[] { "Bi Số 8", "8-Ball", "Bille 8", "Bola 8", "8番ボール", "8번 공", "8号球" } },
            { "rules_tab_5_btn", new[] { "Thắng/Thua", "Win/Loss", "Gagné/Perdu", "Victoria", "勝敗", "승패", "胜负" } },
            { "rules_head_detail", new[] { "Chi tiết điều luật:", "Detailed Rules:", "Détail du règlement :", "Reglamento detallado:", "詳細ルール:", "세부 규칙:", "详细规则：" } },
            { "rules_close_btn", new[] { "ĐÃ HIỂU & ĐÓNG", "GOT IT & CLOSE", "COMPRIS & FERMER", "ENTENDIDO Y CERRAR", "了解して閉じる", "확인 및 닫기", "了解并关闭" } },

            // ===== RULES MODAL: CONTENT =====
            { "rules_tab_1_desc", new[] {
                "• Bi cái được đặt sau lằn ngang phía trên (Head String).\n• Cú đánh khai cuộc hợp lệ phải đưa ít nhất một bi mục tiêu vào lỗ HOẶC có tối thiểu 4 bi mục tiêu chạm vào băng.\n• Nếu không đạt một trong hai điều kiện trên: phạm lỗi Khai Cuộc Không Hợp Lệ (Illegal Break).\n• Rơi bi cái (Scratch) khi khai cuộc: đối thủ nhận quyền Ball-in-Hand phía sau lằn ngang.\n• Theo luật WPA: Dù có bi vào lỗ khi khai cuộc thì bàn vẫn luôn ở trạng thái MỞ (Open Table).",
                "• The cue ball is placed behind the head string.\n• A legal break must either pocket a ball OR drive at least 4 numbered balls to rails.\n• Failing either requirement results in an Illegal Break foul.\n• Scratch on break: opponent receives ball-in-hand behind the head string.\n• Per WPA rules: The table remains OPEN after the break even if balls were pocketed.",
                "• La blanche est placée derrière la ligne de tête.\n• Une casse légale doit empocher une bille OU envoyer au moins 4 billes à la bande.\n• Si ces conditions ne sont pas remplies : Faute de casse illégale.\n• Poche de la blanche : l'adversaire obtient bille en main derrière la ligne.\n• Selon les règles WPA : La table reste OUVERTE après la casse.",
                "• La blanca se coloca detrás de la línea de cabecera.\n• Un saque legal debe embocar una bola O llevar al menos 4 bolas a las bandas.\n• El incumplimiento constituye Falta de Saque Ilegal.\n• Blanca embocada: el rival recibe bola en mano tras la línea.\n• Reglas WPA: La mesa permanece ABIERTA tras el saque.",
                "• 手玉はヘッドストリング（開始線）の後ろに配置します。\n• 有効なブレイクには、いずれかの的球のポケットイン、または4個以上の的球のクッション到達が必要です。\n• 条件を満たさない場合は無効ブレイクファウルとなります。\n• ブレイクスクラッチ時は、相手にヘッドストリング後方からのフリーボールが与えられます。\n• WPA公式規則では、ブレイクで球が入ってもテーブルはオープン状態を維持します。",
                "• 큐볼은 헤드 스트링 뒤에 배치합니다.\n• 합법적인 브레이크는 최소 1개의 공을 넣거나 4개 이상의 공이 쿠션에 닿아야 합니다.\n• 위 조건을 만족하지 못하면 무효 브레이크 파울이 됩니다.\n• 브레이크 스크래치 시 상대는 헤드 스트링 뒤 프리볼을 얻습니다.\n• WPA 규정에 따라 브레이크 시 공이 들어가도 테이블은 항상 오픈 상태입니다.",
                "• 母球置于开球线后。\n• 合法开球必须进球或至少4颗目标球碰库。\n• 未满足上述条件属于无效开球犯规。\n• 开球母球落袋，对手获得开球线后手中球。\n• 根据WPA国际规则，开球即便进球，台面仍然保持开放。"
            } },
            { "rules_tab_2_desc", new[] {
                "• Khi bàn còn MỞ (Open Table), người chơi có quyền chạm bất kỳ bi nào (trơn 1-7 hoặc sọc 9-15, ngoại trừ bi 8 đen) đầu tiên.\n• Nhóm bi được xác định bởi quả bi trơn/sọc đầu tiên vào lỗ (bất kỳ lỗ nào) sau khai cuộc, không cần gọi bi hay gọi lỗ.\n• Cơ thủ đưa bi trơn hợp lệ vào lỗ sẽ được gán nhóm Bi Trơn (Solids 1-7); đối thủ sẽ nhận nhóm Bi Sọc (Stripes 9-15).",
                "• When table is OPEN, players may strike any ball (solids 1-7 or stripes 9-15, except 8-ball) first.\n• Groups are determined by the first legally pocketed ball after the break.\n• Pocketing a solid assigns Solids (1-7) to shooter; opponent receives Stripes (9-15).",
                "• Quand la table est OUVERTE, le joueur peut toucher n'importe quelle bille en premier (sauf la 8).\n• Le groupe est déterminé par la première bille empochée légalement après la casse.\n• Empocher une pleine attribue les Pleines (1-7) ; l'adversaire prend les Rayées (9-15).",
                "• Mientras la mesa esté ABIERTA, se puede golpear cualquier bola primero (excepto la 8).\n• El grupo se define por la primera bola embocada legalmente tras el saque.\n• Embocar una lisa asigna Lisas (1-7); el oponente recibe Rayadas (9-15).",
                "• オープンテーブル時は、8番を除くソリッド(1-7)またはストライプ(9-15)のどちらを先に当てても構いません。\n• ブレイク後、合法的に最初にポケットインした的球によって各プレイヤーのグループが決定されます。\n• ソリッドを入れたプレイヤーはソリッド(1-7)、相手はストライプ(9-15)となります。",
                "• 오픈 테이블일 때는 8번 공을 제외한 단색구(1-7) 또는 줄무늬구(9-15) 중 아무 공이나 먼저 칠 수 있습니다.\n• 브레이크 후 합법적으로 먼저 들어간 공에 의해 각자의 그룹이 지정됩니다.\n• 단색구를 넣은 선수는 단색구(1-7)를, 상대는 줄무늬구(9-15)를 배정받습니다.",
                "• 开放球局时，选手可首先击打除8号黑球外的任意花色球（全色1-7或花色9-15）。\n• 开球后首次合法入袋的目标球决定双方球组。\n• 打入全色球的选手获得全色球组（1-7），对手获得花色球组（9-15）。"
            } },
            { "rules_tab_3_desc", new[] {
                "• <b>Thứ tự chạm bi:</b> Sau khi đã gán nhóm, bi cái phải chạm vào bi thuộc nhóm của mình trước tiên. Chạm bi đối thủ hoặc bi 8 trước sẽ bị phạt lỗi WrongBallFirst.\n• <b>Không chạm bi nào:</b> Bi cái không chạm bất kỳ bi nào (chỉ chạm băng) bị phạt lỗi Không Chạm Bi Nào (No Ball Hit).\n• <b>Quy tắc chạm băng:</b> Sau khi bi cái tiếp xúc bi mục tiêu hợp lệ, phải có ít nhất một bi chạm băng hoặc vào lỗ. Nếu không sẽ bị phạt lỗi NoRailAfterContact.\n• <b>Hết giờ (Shot Clock Expired):</b> Không thực hiện cú đánh khi hết thời gian cho phép.\n• <b>Hình phạt:</b> Mọi lỗi phạm quy đều trao cho đối thủ quyền Đặt Bi Tự Do Trên Toàn Bàn (Ball-in-Hand Anywhere on Table).",
                "• <b>Contact Order:</b> Must hit own group ball first. Hitting opponent's ball or 8-ball first is a foul.\n• <b>No Ball Hit:</b> Cue ball touching no ball at all (rails only) is a No Ball Hit foul.\n• <b>Rail Rule:</b> After legal contact, at least one ball must hit a cushion or be pocketed.\n• <b>Timeout:</b> Failing to shoot before shot clock expires is a foul.\n• <b>Penalty:</b> All fouls award opponent Ball-in-Hand anywhere on the table.",
                "• <b>Ordre de contact :</b> Toucher d'abord une bille de son groupe. Toucher une bille adverse ou la 8 est une faute.\n• <b>Aucune bille touchée :</b> La blanche ne touchant aucune bille (bandes seulement) est une faute.\n• <b>Règle de bande :</b> Après impact, au moins une bille doit toucher une bande ou être empochée.\n• <b>Temps écoulé :</b> Dépasser le temps limite constitue une faute.\n• <b>Pénalité :</b> Toute faute donne bille en main sur toute la table à l'adversaire.",
                "• <b>Orden de impacto:</b> Debe golpear primero una bola de su grupo. Tocar una bola rival o la 8 es falta.\n• <b>Ninguna bola tocada:</b> Si la blanca no toca ninguna bola (solo bandas) es falta.\n• <b>Regla de banda:</b> Tras el impacto, al menos una bola debe tocar banda o embocarse.\n• <b>Tiempo:</b> Agotar el reloj de tiro es falta.\n• <b>Penalización:</b> Toda falta otorga bola en mano en toda la mesa al oponente.",
                "• <b>ファーストヒット:</b> 自身のグループ球に最初に当てなければなりません。相手球や8番に初衝突するとファウルです。\n• <b>ノーヒット:</b> 手玉がどの球にも当たらない（クッションのみ）場合はファウルです。\n• <b>クッション要件:</b> 的球接触後、手玉かいずれかの球がクッションに到達するかポケットインする必要があります。\n• <b>ショットクロック:</b> 制限時間内にショットを打たないとタイムオーバーファウルです。\n• <b>ペナルティ:</b> 全てのファウルで相手プレイヤーにテーブル全域でのフリーボールが与えられます。",
                "• <b>타격 순서:</b> 반드시 자신의 그룹 공을 먼저 쳐야 합니다. 상대 공이나 8번 공을 먼저 치면 파울입니다.\n• <b>타격 없음:</b> 큐볼이 어떤 공도 치지 못하고 쿠션만 닿으면 파울입니다.\n• <b>쿠션 규정:</b> 적구 충돌 후 적어도 1개의 공이 쿠션에 닿거나 포켓되어야 합니다.\n• <b>제한 시간:</b> 샷 타이머가 만료되면 파울이 선언됩니다.\n• <b>벌칙:</b> 모든 파울 시 상대방에게 테이블 전 구역 프리볼 권한이 주어집니다.",
                "• <b>击球顺序：</b>必须首先击打自身球组目标球，错击对手球或8号球判犯规。\n• <b>未击中任何球：</b>母球未碰到任何球（仅碰库）判犯规。\n• <b>碰库规则：</b>母球与目标球接触后，必须至少有一颗球碰库或进袋。\n• <b>击球限时：</b>出杆倒计时归零未击球判超时犯规。\n• <b>判罚：</b>所有犯规均判由对手获得全台任意手中球。"
            } },
            { "rules_tab_4_desc", new[] {
                "• Chỉ khi cơ thủ đã dọn sạch toàn bộ 7 bi thuộc nhóm của mình thì bi số 8 mới trở thành bi mục tiêu hợp lệ.\n• Không cần gọi lỗ: đưa bi số 8 vào bất kỳ lỗ nào (sau khi đã ăn hết nhóm, không phạm lỗi) là thắng ván.\n• <b>Xử THUA VÁN NGAY LẬP TỨC nếu:</b>\n  1. Đưa bi 8 vào lỗ khi chưa ăn hết bi nhóm mình.\n  2. Đưa bi 8 vào lỗ khi bàn còn mở.\n  3. Rơi bi cái (Scratch) trong cú đánh đưa bi số 8 vào lỗ.\n  4. Làm bi số 8 bay văng ra khỏi bàn.",
                "• The 8-ball becomes the legal target only after all 7 balls of player's group are pocketed.\n• Legally pocketing the 8-ball wins the rack.\n• <b>Loss of Rack occurs if:</b>\n  1. Pocketing 8-ball before clearing group balls.\n  2. Pocketing 8-ball when table is open.\n  3. Cue ball scratches when pocketing the 8-ball.\n  4. 8-ball is driven off the table.",
                "• La bille 8 devient cible légale uniquement après avoir empoché ses 7 billes.\n• Empocher légalement la 8 fait gagner la manche.\n• <b>Perte immédiate de la manche si :</b>\n  1. Empocher la 8 avant d'avoir vidé son groupe.\n  2. Empocher la 8 quand la table est ouverte.\n  3. Faire tomber la blanche en empochant la 8.\n  4. Éjecter la bille 8 hors de la table.",
                "• La bola 8 solo es legal tras embocar las 7 bolas del grupo propio.\n• Embocar legalmente la bola 8 otorga la victoria del rack.\n• <b>Pérdida inmediata de la partida si:</b>\n  1. Se emboca la 8 antes de limpiar el grupo.\n  2. Se emboca la 8 con mesa abierta.\n  3. Cae la blanca en el tiro de la bola 8.\n  4. La bola 8 sale despedida de la mesa.",
                "• 自身のグループ的球7個を全てポケットして初めて、8番ボールが有効な的球となります。\n• 8番ボールを合法的にポケットインするとそのラックの勝利となります。\n• <b>以下の場合、直ちにそのラックの敗戦となります:</b>\n  1. グループ的球が残っている状態で8番を入れた場合。\n  2. オープンテーブル時に8番を入れた場合。\n  3. 8番を入れたショットで手玉がスクラッチした場合。\n  4. 8番ボールをテーブル外に場外させた場合。",
                "• 자신의 그룹 공 7개를 모두 넣은 후에만 8번 공이 합법적인 목적구가 됩니다.\n• 파울 없이 8번 공을 포켓에 넣으면 해당 랙에서 승리합니다.\n• <b>다음의 경우 즉시 해당 랙 패배 처리됩니다:</b>\n  1. 자신의 그룹 공이 남아있는데 8번 공을 넣은 경우.\n  2. 테이블이 오픈 상태일 때 8번 공을 넣은 경우.\n  3. 8번 공을 넣는 샷에서 큐볼 스크래치가 발생한 경우.\n  4. 8번 공이 테이블 밖으로 튕겨 나간 경우.",
                "• 只有击落己方全部7颗组内目标球后，8号球方成为合法目标球。\n• 合法击入8号球赢得本局比赛。\n• <b>出现下列情况将直接判负本局：</b>\n  1. 己方球未打完前击落8号球。\n  2. 开放球局时击落8号球。\n  3. 击落8号球的同时母球落袋。\n  4. 导致8号球飞出球台。"
            } },
            { "rules_tab_5_desc", new[] {
                "• Cơ thủ dọn sạch nhóm bi của mình và đưa bi số 8 vào lỗ hợp lệ sẽ GIÀNH CHIẾN THẮNG VÁN ĐẤU.\n• Trận đấu diễn ra theo thể thức Race-to (chạm đích):\n  Cơ thủ đầu tiên đạt đủ số ván thắng theo quy định (Race to 3, 5, 7, hoặc 9) sẽ trở thành NGƯỜI CHIẾN THẮNG CHUNG CUỘC.",
                "• Legally pocketing all group balls and the 8-ball wins the rack.\n• Match format is Race-to:\n  The first player to reach the target number of rack wins (Race to 3, 5, 7, or 9) wins the entire match.",
                "• Vider son groupe et empocher la 8 remporte la manche.\n• La partie se joue au premier arrivé (Race-to) :\n  Le premier joueur atteignant le nombre de manches requis (3, 5, 7 ou 9) est déclaré GRAND VAINQUEUR.",
                "• Limpiar el grupo y embocar legalmente la 8 gana el rack.\n• Formato por objetivos (Race-to):\n  El primer jugador en alcanzar las partidas fijadas (Al 3, 5, 7 o 9) se corona CAMPEÓN.",
                "• 自身のグループ球を全て落とし、8番ボールを合法的に入れることでそのラックを獲得します。\n• 試合は先取形式（Race-to）で行われます:\n  規定のラック数（3、5、7、9ラック先取）に最初に到達した選手が総合優勝となります。",
                "• 자신의 그룹 공을 모두 정리하고 8번 공을 정상적으로 넣으면 해당 랙을 승리합니다.\n• 경기는 선승제(Race-to) 방식으로 진행됩니다:\n  규정된 승수(3, 5, 7, 9선승)에 먼저 도달한 선수가 최종 우승을 차지합니다.",
                "• 清空本组目标球并合法击入8号球将赢得本局。\n• 比赛采用局数抢胜赛制（Race-to）：\n  率先达到规定胜局数（抢3、5、7或9局）的选手获得整场比赛的最终总冠军。"
            } },

            // ===== HELP MODAL =====
            { "help_title", new[] { "HƯỚNG DẪN ĐIỀU KHIỂN & PHÍM TẮT", "CONTROLS & SHORTCUTS GUIDE", "GUIDE DES COMMANDES & RACCOURCIS", "GUÍA DE CONTROLES Y ATAJOS", "操作方法とショートカット一覧", "조작 가이드 및 단축키 안내", "操作指南与快捷键" } },
            { "help_content", new[] {
                "<b>HƯỚNG DẪN ĐIỀU KHIỂN & THAO TÁC GAME BIDA 3D:</b>\n\n• <b>Xoay hướng ngắm:</b> Di chuyển chuột để xoay camera 360 độ quanh bi cái.\n• <b>Chỉnh lực đánh:</b> Lăn con lăn chuột (Mouse ScrollWheel) từ 5% đến 100%.\n• <b>Bắn bi:</b> Nhấp chuột trái (Left Mouse Button).\n• <b>Chọn điểm đánh trên bi cái (Spin):</b> Nhấn phím [J] để mở bảng chọn, click/kéo hoặc dùng mũi tên để chọn điểm, [R] về giữa, [Enter] để xác nhận.\n• <b>Nâng cơ / nhảy bi:</b> Nhấn phím [K], kéo cơ quanh bi (0°–60°) hoặc dùng mũi tên ↑↓, [R] về 0°, [Enter] để chốt.\n• <b>Đặt bi tự do (Ball-in-Hand):</b> Dùng [W, A, S, D] để di chuyển bi cái, nhấn [Enter] để xác nhận.\n• <b>Bật/tắt con trỏ chuột:</b> Nhấn phím [Alt] để hiện con trỏ thao tác các nút giao diện.\n• <b>Phím tắt:</b> [Esc] Cài đặt, [Tab] Bảng thống kê, [R] Luật thi đấu, [H] Trợ giúp.",
                "<b>3D BILLIARDS CONTROLS & SHORTCUTS GUIDE:</b>\n\n• <b>Aim Rotation:</b> Move mouse to orbit camera 360° around cue ball.\n• <b>Power Adjustment:</b> Mouse ScrollWheel (5% to 100%).\n• <b>Shoot:</b> Left Click (LMB).\n• <b>Cue Ball Spin (English):</b> Press [J] to open spin selector, click/drag or arrow keys, [R] reset, [Enter] confirm.\n• <b>Cue Elevation / Jump:</b> Press [K], drag elevation angle (0°–60°) or arrow keys ↑↓, [R] reset, [Enter] confirm.\n• <b>Ball-in-Hand Placement:</b> Use [W, A, S, D] to position cue ball, press [Enter] to place.\n• <b>Toggle Cursor:</b> Press [Alt] to show/hide mouse cursor for UI buttons.\n• <b>Shortcuts:</b> [Esc] Settings, [Tab] Match Stats, [R] Rules, [H] Help.",
                "<b>GUIDE DES COMMANDES DE BILLARD 3D :</b>\n\n• <b>Visée :</b> Bougez la souris pour tourner à 360° autour de la blanche.\n• <b>Puissance :</b> Molette de la souris (5% à 100%).\n• <b>Tirer :</b> Clic gauche.\n• <b>Effet (Spin) :</b> Touche [J], clic/glisser ou flèches, [R] réinitialiser, [Entrée] valider.\n• <b>Élévation / Saut :</b> Touche [K], angle (0°–60°), [R] 0°, [Entrée] valider.\n• <b>Bille en main :</b> Utilisez [Z, Q, S, D] ou flèches, [Entrée] pour poser.\n• <b>Curseur :</b> Appuyez sur [Alt] pour afficher/masquer la souris.\n• <b>Raccourcis :</b> [Echap] Menu, [Tab] Stats, [R] Règles, [H] Aide.",
                "<b>GUÍA DE CONTROLES DE BILLAR 3D:</b>\n\n• <b>Rotación de mira:</b> Mueve el ratón para rotar 360° alrededor de la blanca.\n• <b>Fuerza de tiro:</b> Rueda del ratón (5% a 100%).\n• <b>Tirar:</b> Clic izquierdo.\n• <b>Efecto (Spin):</b> Tecla [J], arrastra o flechas, [R] centrar, [Enter] confirmar.\n• <b>Elevación / Salto:</b> Tecla [K], ángulo (0°–60°), [R] 0°, [Enter] confirmar.\n• <b>Bola en mano:</b> Usa [W, A, S, D] para colocar la bola, [Enter] para fijar.\n• <b>Cursor:</b> Pulsa [Alt] para mostrar/ocultar el cursor.\n• <b>Atajos:</b> [Esc] Ajustes, [Tab] Estadísticas, [R] Reglas, [H] Ayuda.",
                "<b>3Dビリヤード 操作方法とショートカットガイド:</b>\n\n• <b>視点・エイム回転:</b> マウス移動で手玉を中心に360度カメラ回転。\n• <b>ショットパワー調整:</b> マウスホイール回転（5%〜100%）。\n• <b>ショット:</b> マウス左クリック。\n• <b>スピン（撞点）設定:</b> [J]キーで設定画面を開き、ドラッグまたは矢印キーで選択、[R]で中心、[Enter]で確定。\n• <b>キュー仰角（ジャンプショット）:</b> [K]キーで角度設定(0°〜60°)、[R]で0°リセット、[Enter]で確定。\n• <b>フリーボール配置:</b> [W, A, S, D]キーで手玉を移動、[Enter]で配置決定。\n• <b>カーソル表示切替:</b> [Alt]キーでUI操作用マウスカーソルの表示/非表示を切り替え。\n• <b>ショートカット:</b> [Esc] 設定, [Tab] スタッツ, [R] ルール, [H] ヘルプ。",
                "<b>3D 당구 조작 방법 및 단축키 안내:</b>\n\n• <b>조준 회전:</b> 마우스를 움직여 큐볼 주변 360도 카메라 회전.\n• <b>샷 파워 조절:</b> 마우스 휠 스크롤 (5% ~ 100%).\n• <b>샷 실행:</b> 마우스 좌클릭.\n• <b>당점(스핀) 조절:</b> [J] 키를 눌러 당점 선택창 열기, 드래그 또는 방향키, [R] 중앙 초기화, [Enter] 확정.\n• <b>큐 각도(점프샷):</b> [K] 키를 눌러 각도 조절(0°~60°), [R] 0° 초기화, [Enter] 확정.\n• <b>프리볼 배치:</b> [W, A, S, D] 키로 큐볼 위치 이동, [Enter] 키로 배치 완료.\n• <b>마우스 커서 토글:</b> [Alt] 키로 UI 클릭용 마우스 커서 켜기/끄기.\n• <b>단축키:</b> [Esc] 설정, [Tab] 경기 통계, [R] 규정, [H] 도움말.",
                "<b>3D 台球操作指南与快捷键：</b>\n\n• <b>瞄准旋转：</b>移动鼠标围绕母球360度旋转视角。\n• <b>击球力度：</b>滚动鼠标滚轮调节力度（5%至100%）。\n• <b>出杆击球：</b>鼠标左键点击。\n• <b>母球旋转（加塞）：</b>按 [J] 键打开塞球面板，拖拽或方向键选择，[R] 居中，[回车] 确定。\n• <b>抬高球杆（跳球）：</b>按 [K] 键调节仰角（0°至60°），[R] 归零，[回车] 确定。\n• <b>自由球摆放：</b>使用 [W, A, S, D] 移动母球位置，按 [回车] 摆放。\n• <b>显示/隐藏鼠标：</b>按 [Alt] 键切换鼠标光标以便点击界面按钮。\n• <b>快捷键：</b>[Esc] 设置, [Tab] 比赛统计, [R] 规则, [H] 帮助。"
            } },

            // ===== HELP: PHÍM TẮT (dạng [PHÍM] + chức năng) =====
            { "help_k_mouse", new[] { "CHUỘT", "MOUSE", "SOURIS", "RATÓN", "マウス", "마우스", "鼠标" } },
            { "help_k_scroll", new[] { "LĂN", "SCROLL", "MOLETTE", "RUEDA", "ホイール", "휠", "滚轮" } },
            { "help_k_click", new[] { "CLICK", "CLICK", "CLIC", "CLIC", "クリック", "클릭", "左键" } },
            { "help_aim", new[] { "Xoay hướng ngắm", "Aim / rotate view", "Viser / tourner", "Apuntar / rotar", "照準を回転", "조준 회전", "瞄准转向" } },
            { "help_power", new[] { "Chỉnh lực đánh", "Adjust power", "Régler la puissance", "Ajustar fuerza", "パワー調整", "파워 조절", "调节力度" } },
            { "help_shoot", new[] { "Bắn bi", "Shoot", "Tirer", "Tirar", "ショット", "샷", "击球" } },
            { "help_spin", new[] { "Điểm đánh / xoáy", "Spin (English)", "Effet (spin)", "Efecto (spin)", "スピン設定", "당점 / 스핀", "加塞 / 旋转" } },
            { "help_elev", new[] { "Nâng cơ / nhảy bi", "Cue lift / jump", "Élévation / saut", "Elevación / salto", "キュー角度 / ジャンプ", "큐 각도 / 점프", "抬杆 / 跳球" } },
            { "help_camera", new[] { "Camera gần / xa", "Near / far camera", "Caméra proche / loin", "Cámara cerca / lejos", "視点 近 / 遠", "시점 근 / 원", "视角远近" } },
            { "help_place", new[] { "Đặt bi tự do", "Place cue ball", "Placer la blanche", "Colocar la blanca", "手玉を配置", "큐볼 배치", "摆放母球" } },
            { "help_confirm", new[] { "Xác nhận", "Confirm", "Valider", "Confirmar", "決定", "확인", "确认" } },
            { "help_cursor", new[] { "Hiện / ẩn con trỏ", "Show / hide cursor", "Afficher le curseur", "Mostrar cursor", "カーソル切替", "커서 전환", "显示/隐藏鼠标" } },
            { "help_stats", new[] { "Bảng thống kê", "Match stats", "Statistiques", "Estadísticas", "スタッツ", "경기 통계", "比赛统计" } },
            { "help_rules", new[] { "Luật thi đấu", "Rules", "Règles", "Reglas", "ルール", "규정", "规则" } },
            { "help_help", new[] { "Trợ giúp", "Help", "Aide", "Ayuda", "ヘルプ", "도움말", "帮助" } },
            { "help_settings", new[] { "Cài đặt", "Settings", "Paramètres", "Ajustes", "設定", "설정", "设置" } },

            // ===== MATCH HEADER & TV =====
            { "header_race_rack", new[] { "RACE TO {0}  •  VÁN {1}", "RACE TO {0}  •  RACK {1}", "PREMIER À {0}  •  MANCHE {1}", "AL PRIMERO DE {0}  •  PARTIDA {1}", "{0}ラック先取  •  第{1}ラック", "{0}선승  •  {1}랙", "抢 {0} 局  •  第 {1} 局" } },
            { "tv_info_format", new[] { "8-BALL  •  VÁN {0}  •  RACE TO {1}", "8-BALL  •  RACK {0}  •  RACE TO {1}", "8-BALL  •  MANCHE {0}  •  PREMIER À {1}", "8-BALL  •  PARTIDA {0}  •  AL {1}", "8-BALL  •  第{0}ラック  •  {1}先取", "8-BALL  •  {0}랙  •  {1}선승", "8-BALL  •  第 {0} 局  •  抢 {1}" } },

            // ===== CUE ELEVATION =====
            { "elev_title", new[] { "GÓC CƠ", "CUE ELEVATION", "ANGLE DU QUEUE", "ÁNGULO DEL TACO", "キュー角度", "큐 각도", "球杆仰角" } },
            { "elev_popup_title", new[] { "NÂNG CƠ / NHẢY BI", "CUE ELEVATION & JUMP", "ÉLÉVATION & SAUT", "ELEVACIÓN Y SALTO", "ジャンプショット設定", "점프샷 각도 설정", "架杆与跳球设置" } },
            { "elev_normal", new[] { "THƯỜNG", "NORMAL", "NORMAL", "NORMAL", "通常", "일반", "常规" } },
            { "elev_jump", new[] { "NHẢY BI", "JUMP SHOT", "SAUT", "SALTO", "ジャンプ", "점프샷", "跳球" } },
            { "elev_height", new[] { "ĐỘ CAO", "MAX HEIGHT", "HAUTEUR", "ALTURA", "最高高度", "최고 높이", "最大高度" } },
            { "elev_range", new[] { "TẦM BAY", "JUMP RANGE", "DISTANCE", "DISTANCIA", "飛距離", "도약 거리", "跳跃距离" } },
            { "elev_desc_jump", new[] { "Bi cái sẽ bật lên khỏi mặt nỉ khi đánh", "Cue ball will jump over obstacles", "La blanche va sauter au-dessus des billes", "La bola blanca saltará sobre obstáculos", "手玉が障害球を飛び越えます", "큐볼이 장애물을 뛰어넘습니다", "母球将跃过障碍球" } },
            { "elev_desc_low", new[] { "Chưa đủ nâng để nhảy bi (cần từ {0}° trở lên)", "Elevation too low for jump (needs {0}°+)", "Élévation insuffisante (min {0}°)", "Elevación insuficiente (mín {0}°)", "ジャンプには角度不足です ({0}°以上必要)", "점프에 각도가 부족합니다 ({0}° 이상 필요)", "仰角不足无法跳球（需 {0}° 以上）" } },
            { "elev_hint", new[] { "Kéo chuột quanh bi  •  Mũi tên: tinh chỉnh  •  R: về 0°  •  ENTER: chốt  •  ESC: đóng", "Drag around ball • Arrows: tune • R: 0° • ENTER: confirm • ESC: close", "Glisser autour • Flèches : ajuster • R : 0° • ENTRÉE : valider • ECH : fermer", "Arrastrar alrededor • Flechas: ajustar • R: 0° • ENTER: fijar • ESC: cerrar", "球周りをドラッグ • 矢印: 微調整 • R: 0°リセット • ENTER: 決定 • ESC: 閉じる", "공 주위 드래그 • 방향키: 미세조정 • R: 0° 초기화 • ENTER: 결정 • ESC: 닫기", "拖拽调整仰角 • 方向键微调 • R键归零 • 回车确定 • ESC关闭" } },
            { "spin_popup_hint", new[] { "Click / kéo để chọn  •  Mũi tên: tinh chỉnh  •  R: về giữa  •  ENTER: xác nhận", "Click / drag to pick • Arrows: tune • R: center • ENTER: confirm", "Clic / glisser pour choisir • Flèches : ajuster • R : centre • ENTRÉE : valider", "Clic / arrastrar para elegir • Flechas: ajustar • R: centro • ENTER: confirmar", "クリック/ドラッグで選択 • 矢印: 微調整 • R: 中心 • ENTER: 決定", "클릭/드래그로 선택 • 방향키: 미세조정 • R: 중앙 • ENTER: 확인", "点击或拖拽选取 • 方向键微调 • R键居中 • 回车确认" } },

            // ===== STATS SCREEN =====
            { "stats_tab_match", new[] { "CẢ TRẬN", "FULL MATCH", "MATCH COMPLET", "PARTIDA COMPLETA", "試合全体", "전체 매치", "全场统计" } },
            { "stats_tab_rack", new[] { "VÁN HIỆN TẠI", "CURRENT RACK", "MANCHE ACTUELLE", "PARTIDA ACTUAL", "現在のラック", "현재 랙", "当前局" } },
            { "stats_hint_close", new[] { "[TAB] hoặc [ESC] để đóng", "[TAB] or [ESC] to close", "[TAB] ou [ESC] pour fermer", "[TAB] o [ESC] para cerrar", "[TAB] または [ESC] で閉じる", "[TAB] 또는 [ESC]로 닫기", "按 [TAB] 或 [ESC] 关闭" } },
            { "stats_winner_badge", new[] { "CHIẾN THẮNG", "WINNER", "VAINQUEUR", "GANADOR", "勝者", "승자", "获胜者" } },
            { "stats_match_over", new[] { "TRẬN ĐẤU KẾT THÚC", "MATCH OVER", "MATCH TERMINÉ", "FIN DE PARTIDA", "試合終了", "경기 종료", "比赛结束" } },
            { "stats_btn_rematch", new[] { "CHƠI LẠI", "REMATCH", "REVANCHE", "REVANCHA", "再戦", "재경기", "再来一局" } },
            { "stats_btn_lobby", new[] { "VỀ SẢNH", "LOBBY", "MENU", "MENÚ", "ロビーへ", "로비로", "返回大厅" } },

            // ===== PRE-MATCH / DIFFICULTY OPTIONS =====
            { "opt_race_3", new[] { "Race to 3 (Chạm 3)", "Race to 3", "Premier à 3", "Al primero de 3", "3ラック先取", "3선승", "抢 3 局" } },
            { "opt_race_5", new[] { "Race to 5 (Chạm 5)", "Race to 5", "Premier à 5", "Al primero de 5", "5ラック先取", "5선승", "抢 5 局" } },
            { "opt_race_7", new[] { "Race to 7 (Chạm 7)", "Race to 7", "Premier à 7", "Al primero de 7", "7ラック先取", "7선승", "抢 7 局" } },
            { "opt_race_9", new[] { "Race to 9 (Chạm 9)", "Race to 9", "Premier à 9", "Al primero de 9", "9ラック先取", "9선승", "抢 9 局" } },
            { "opt_timer_unlimited", new[] { "Không giới hạn", "Unlimited", "Sans limite", "Sin límite", "無制限", "무제한", "无限制" } },
            { "opt_timer_30", new[] { "30 Giây / Cú đánh", "30 Seconds / Shot", "30s / Coup", "30s / Tiro", "30秒 / ショット", "30초 / 샷", "30 秒 / 击球" } },
            { "opt_timer_45", new[] { "45 Giây / Cú đánh", "45 Seconds / Shot", "45s / Coup", "45s / Tiro", "45秒 / ショット", "45초 / 샷", "45 秒 / 击球" } },
            { "opt_timer_60", new[] { "60 Giây / Cú đánh", "60 Seconds / Shot", "60s / Coup", "60s / Tiro", "60秒 / ショット", "60초 / 샷", "60 秒 / 击球" } },
            { "default_player_name", new[] { "Cơ Thủ", "Player", "Joueur", "Jugador", "プレイヤー", "플레이어", "选手" } },
            { "ai_easy_name", new[] { "AI (Dễ)", "AI (Easy)", "IA (Facile)", "IA (Fácil)", "AI (かんたん)", "AI (쉬움)", "人机 (简单)" } },
            { "ai_normal_name", new[] { "AI (Bình Thường)", "AI (Normal)", "IA (Normal)", "IA (Normal)", "AI (ふつう)", "AI (보통)", "人机 (普通)" } },
            { "ai_hard_name", new[] { "AI (Khó)", "AI (Hard)", "IA (Difficile)", "IA (Difícil)", "AI (むずかしい)", "AI (어려움)", "人机 (困难)" } },

            // ===== ONLINE ROOM UI =====
            { "online_panel_title", new[] { "PHÒNG ĐẤU TRỰC TUYẾN (ONLINE PVP)", "ONLINE MATCHMAKING (PVP)", "PARTIE EN LIGNE (PVP)", "SALA EN LÍNEA (PVP)", "オンライン対戦ルーム", "온라인 대전 룸", "在线对战大厅" } },
            { "online_connected", new[] { "● Đã kết nối Photon Master Server", "● Connected to Photon Master Server", "● Connecté au serveur Photon", "● Conectado al servidor Photon", "● Photonサーバー接続完了", "● Photon 마스터 서버 연결됨", "● 已连接 Photon 服务器" } },
            { "online_disconnected", new[] { "○ Chưa kết nối máy chủ Photon", "○ Disconnected from Photon Server", "○ Déconnecté du serveur", "○ Desconectado del servidor", "○ サーバー未接続", "○ 서버 연결 끊김", "○ 未连接服务器" } },
            { "online_connecting", new[] { "Đang kết nối tới máy chủ Photon...", "Connecting to Photon Server...", "Connexion au serveur Photon...", "Conectando al servidor Photon...", "Photonサーバーに接続中...", "서버에 연결 중...", "正在连接服务器..." } },
            { "online_creating", new[] { "Đang tạo phòng mới...", "Creating new room...", "Création du salon...", "Creando nueva sala...", "ルーム作成中...", "방 생성 중...", "正在创建房间..." } },
            { "online_joining", new[] { "Đang tham gia phòng {0}...", "Joining room {0}...", "Rejoindre salon {0}...", "Uniéndose a sala {0}...", "ルーム {0} に参加中...", "방 {0}에 참가 중...", "正在加入房间 {0}..." } },
            { "online_copied", new[] { "Đã sao chép mã phòng vào clipboard!", "Room code copied to clipboard!", "Code copié dans le presse-papiers !", "¡Código de sala copiado!", "ルームIDをコピーしました！", "방 코드가 클립보드에 복사되었습니다!", "房间邀请码已复制到剪贴板！" } },
            { "online_ready", new[] { "ĐÃ SẴN SÀNG", "READY", "PRÊT", "LISTO", "準備完了", "준비 완료", "已准备" } },
            { "online_not_ready", new[] { "CHƯA SẴN SÀNG", "NOT READY", "PAS PRÊT", "NO LISTO", "待機中", "준비 중", "未准备" } },
            { "online_waiting", new[] { "Đang chờ đối thủ tham gia...", "Waiting for opponent to join...", "En attente d'un adversaire...", "Esperando a otro jugador...", "対戦相手の参加を待っています...", "상대방 입장을 기다리는 중...", "正在等待对手加入..." } },
            { "online_btn_create", new[] { "TẠO PHÒNG MỚI", "CREATE ROOM", "CRÉER UN SALON", "CREAR SALA", "ルーム作成", "방 만들기", "创建房间" } },
            { "online_btn_join", new[] { "VÀO PHÒNG", "JOIN ROOM", "REJOINDRE", "UNIRSE", "参加する", "참가하기", "加入房间" } },
            { "online_btn_connect", new[] { "KẾT NỐI", "CONNECT", "CONNEXION", "CONECTAR", "接続", "연결", "连接" } },
            { "online_btn_start", new[] { "BẮT ĐẦU TRẬN ĐẤU", "START MATCH", "LANCER LE MATCH", "INICIAR PARTIDA", "対戦開始", "매치 시작", "开始对战" } },
            { "online_btn_leave", new[] { "RỜI PHÒNG", "LEAVE ROOM", "QUITTER SALON", "SALIR DE SALA", "退室", "방 나가기", "离开房间" } },
            { "online_btn_back", new[] { "QUAY LẠI", "BACK", "RETOUR", "VOLVER", "戻る", "뒤로", "返回" } },
            { "online_btn_ready", new[] { "SẴN SÀNG", "READY", "PRÊT", "LISTO", "準備完了", "준비", "准备" } },
            { "online_role_host", new[] { "CHỦ PHÒNG", "HOST", "HÔTE", "ANFITRIÓN", "ホスト", "방장", "房主" } },
            { "online_role_guest", new[] { "KHÁCH", "GUEST", "INVITÉ", "INVITADO", "ゲスト", "게스트", "客人" } },
            { "online_you", new[] { "BẠN", "YOU", "VOUS", "TÚ", "あなた", "나", "你" } },
            { "online_slot_empty", new[] { "ĐANG CHỜ ĐỐI THỦ...", "WAITING FOR OPPONENT...", "EN ATTENTE...", "ESPERANDO RIVAL...", "対戦相手を待機中...", "상대 대기 중...", "等待对手..." } },
            { "settings_player_name", new[] { "TÊN CƠ THỦ", "PLAYER NAME", "NOM DU JOUEUR", "NOMBRE DEL JUGADOR", "プレイヤー名", "플레이어 이름", "玩家名称" } },
            { "settings_player_name_hint", new[] { "Nhập tên của bạn...", "Enter your name...", "Entrez votre nom...", "Introduce tu nombre...", "名前を入力...", "이름을 입력하세요...", "输入你的名字..." } },
            { "settings_avatar", new[] { "ẢNH ĐẠI DIỆN", "AVATAR", "AVATAR", "AVATAR", "アバター", "아바타", "头像" } },
            { "settings_change_avatar", new[] { "ĐỔI ẢNH", "CHANGE", "CHANGER", "CAMBIAR", "変更", "변경", "更换" } },
            { "avatar_picker_title", new[] { "CHỌN ẢNH ĐẠI DIỆN", "CHOOSE AVATAR", "CHOISIR UN AVATAR", "ELEGIR AVATAR", "アバターを選択", "아바타 선택", "选择头像" } },

            // ===== SPIN (ENGLISH) SELECTOR =====
            { "spin_center", new[] { "Giữa", "Center", "Centre", "Centro", "センター", "중앙", "居中" } },
            { "spin_top", new[] { "Cu-lê", "Follow (Top)", "Coulé", "Corrido", "押し球 (トップ)", "밀어치기", "高杆 (上塞)" } },
            { "spin_bottom", new[] { "Trô", "Draw (Bottom)", "Rétro", "Retroceso", "引き球 (ボトム)", "끌어치기", "低杆 (下塞)" } },
            { "spin_right", new[] { "Phải", "Right", "Droite", "Derecha", "右", "우", "右塞" } },
            { "spin_left", new[] { "Trái", "Left", "Gauche", "Izquierda", "左", "좌", "左塞" } },
            { "spin_title", new[] { "ĐIỂM CƠ [J]", "CUE BALL SPIN [J]", "EFFET BILLE [J]", "EFECTO BOLA [J]", "スピン（撞点） [J]", "당점 조절 [J]", "击球点 [J]" } },
            { "spin_popup_title", new[] { "CHỌN ĐIỂM ĐÁNH TRÊN BI CÁI", "CUE BALL SPIN SELECTOR", "SÉLECTEUR D'EFFET DE LA BLANCHE", "SELECTOR DE EFECTO", "手玉のスピン・撞点選択", "큐볼 당점(스핀) 설정", "选择母球击球点" } },

            // ===== HUD TOASTS & PLAYER DEFAULTS =====
            { "hud_match_started", new[] { "TRẬN ĐẤU BẮT ĐẦU!", "MATCH STARTED!", "LE MATCH COMMENCE !", "¡PARTIDA INICIADA!", "試合開始！", "경기 시작!", "比赛开始！" } },
            { "hud_break_shot_toast", new[] { "LƯỢT BẮN: {0} KHAI CUỘC (BREAK SHOT)!", "SHOOTING: {0} BREAK SHOT!", "AU TOUR DE : {0} (CASSE INITIALE) !", "TURNO: ¡{0} SAQUE INICIAL!", "ショット: {0} ブレイク！", "차례: {0} 선공 브레이크!", "击球：{0} 开球！" } },
            { "hud_match_champion", new[] { "VÔ ĐỊCH TOÀN BỘ TRẬN ĐẤU!", "MATCH CHAMPION!", "CHAMPION DU MATCH !", "¡CAMPEÓN DEL ENCUENTRO!", "大会総合優勝！", "매치 최종 우승!", "夺得比赛总冠军！" } },
            { "hud_default_p1", new[] { "CƠ THỦ 1 (BẠN)", "PLAYER 1 (YOU)", "JOUEUR 1 (VOUS)", "JUGADOR 1 (TÚ)", "プレイヤー1 (あなた)", "플레이어 1 (나)", "选手 1 (您)" } },
            { "hud_default_p2", new[] { "CƠ THỦ 2 (AI)", "PLAYER 2 (AI)", "JOUEUR 2 (IA)", "JUGADOR 2 (IA)", "プレイヤー2 (AI)", "플레이어 2 (AI)", "选手 2 (人机)" } },

            // ===== DIFFICULTY & PRE-MATCH CONFIG =====
            { "diff_level_header", new[] { "ĐỘ KHÓ", "DIFFICULTY", "DIFFICULTÉ", "DIFICULTAD", "難易度", "난이도", "难度" } },
            { "online_config_summary", new[] { "CẤU HÌNH TRẬN ĐẤU", "MATCH CONFIGURATION", "CONFIGURATION DU MATCH", "CONFIGURACIÓN DE PARTIDA", "対戦マッチ設定", "경기 설정", "对战设置" } },
            { "online_config_title", new[] { "CẤU HÌNH TRẬN ĐẤU", "MATCH CONFIGURATION", "CONFIGURATION DU MATCH", "CONFIGURACIÓN DE PARTIDA", "対戦マッチ設定", "경기 설정", "对战设置" } },
            { "btn_confirm", new[] { "XÁC NHẬN", "CONFIRM", "CONFIRMER", "CONFIRMAR", "確定", "확인", "确认" } },

            // ===== ONLINE ROOM MESSAGES =====
            { "online_status_ready", new[] { "Sẵn sàng tạo phòng hoặc tham gia bằng Room ID", "Ready to create or join room by ID", "Prêt à créer ou rejoindre avec l'ID", "Listo para crear o unirse con ID", "ルーム作成またはIDで参加可能", "방 만들기 또는 ID로 참가 준비 완료", "已就绪，可创建房间或输入ID加入" } },
            { "online_id_invalid", new[] { "Vui lòng nhập ID phòng hợp lệ!", "Please enter a valid Room ID!", "Veuillez saisir un ID valide !", "¡Por favor introduce una ID válida!", "有効なルームIDを入力してください！", "올바른 방 ID를 입력하세요!", "请输入有效的房间ID！" } },
            { "online_searching_room", new[] { "Đang tìm phòng {0}...", "Finding room {0}...", "Recherche du salon {0}...", "Buscando sala {0}...", "ルーム {0} を検索中...", "방 {0} 찾는 중...", "正在查找房间 {0}..." } },
            { "online_room_title", new[] { "PHÒNG: <color=#f1c40f>{0}</color>", "ROOM: <color=#f1c40f>{0}</color>", "SALON : <color=#f1c40f>{0}</color>", "SALA: <color=#f1c40f>{0}</color>", "ルーム: <color=#f1c40f>{0}</color>", "룸: <color=#f1c40f>{0}</color>", "房间：<color=#f1c40f>{0}</color>" } },
            { "online_settings_info", new[] { "Luật WPA 8-Ball | Chạm {0} | {1}", "WPA 8-Ball | Race to {0} | {1}", "WPA 8-Ball | Premier à {0} | {1}", "WPA 8-Ball | Al {0} | {1}", "WPA 8ボール | {0}先取 | {1}", "WPA 8볼 | {0}선승 | {1}", "WPA 8球 | 抢 {0} | {1}" } },
            { "online_host_name", new[] { "[Chủ phòng] {0}", "[Host] {0}", "[Hôte] {0}", "[Anfitrión] {0}", "[ホスト] {0}", "[방장] {0}", "[房主] {0}" } },
            { "online_guest_name", new[] { "[Khách] {0}", "[Guest] {0}", "[Invité] {0}", "[Invitado] {0}", "[ゲスト] {0}", "[게스트] {0}", "[客席] {0}" } },
            { "online_cancel_ready", new[] { "HỦY SẴN SÀNG", "CANCEL READY", "ANNULER", "CANCELAR", "準備解除", "준비 취소", "取消准备" } },
            { "online_msg_both_ready_start", new[] { "Cả 2 đã sẵn sàng! Hãy bấm BẮT ĐẦU TRẬN.", "Both players ready! Click START MATCH.", "Les 2 sont prêts ! Cliquez sur LANCER.", "¡Ambos listos! Pulsa INICIAR PARTIDA.", "双方の準備完了！「対戦開始」を押してください。", "양쪽 모두 준비 완료! 경기를 시작하세요.", "双方已准备就绪！请点击开始对战。" } },
            { "online_msg_both_ready_wait", new[] { "Cả 2 đã sẵn sàng! Đang chờ chủ phòng bắt đầu...", "Both ready! Waiting for host to start...", "Prêts ! En attente de l'hôte...", "¡Ambos listos! Esperando al anfitrión...", "準備完了！ホストの試合開始を待機中...", "준비 완료! 방장이 경기를 시작하기를 기다리는 중...", "双方已准备就绪！等待房主开始对战..." } },
            { "online_msg_click_ready", new[] { "Vui lòng bấm Sẵn sàng để chuẩn bị vào trận.", "Please click Ready to prepare for match.", "Cliquez sur Prêt pour vous préparer.", "Pulsa Listo para prepararte.", "「準備完了」ボタンを押してください。", "경기 준비를 위해 [준비 완료]를 누르세요.", "请点击准备按钮以准备进入比赛。" } },
            { "online_msg_wait_opponent", new[] { "Đang chờ đủ 2 người chơi và sẵn sàng...", "Waiting for 2 players to be ready...", "En attente de 2 joueurs prêts...", "Esperando a 2 jugadores listos...", "2人のプレイヤーが準備完了するのを待機中...", "2명의 플레이어가 준비되기를 기다리는 중...", "正在等待2名玩家就绪..." } },

            // ===== STATS & MATCH TIMING =====
            { "stats_info_match", new[] { "RACE TO {0}  •  TỔNG {1} VÁN  •  THỜI GIAN TRẬN {2}", "RACE TO {0}  •  TOTAL {1} RACKS  •  MATCH TIME {2}", "PREMIER À {0}  •  TOTAL {1} MANCHES  •  DURÉE {2}", "AL {0}  •  TOTAL {1} PARTIDAS  •  TIEMPO {2}", "{0}先取  •  全{1}ラック  •  試合時間 {2}", "{0}선승  •  총 {1}랙  •  경기 시간 {2}", "抢 {0}  •  共 {1} 局  •  比赛总耗时 {2}" } },
            { "stats_info_rack", new[] { "RACE TO {0}  •  VÁN {1}  •  TRẬN {2}  •  VÁN {3}", "RACE TO {0}  •  RACK {1}  •  MATCH {2}  •  RACK {3}", "PREMIER À {0}  •  MANCHE {1}  •  MATCH {2}  •  MANCHE {3}", "AL {0}  •  PARTIDA {1}  •  MATCH {2}  •  RACK {3}", "{0}先取  •  第{1}ラック  •  試合 {2}  •  ラック {3}", "{0}선승  •  {1}랙  •  경기 {2}  •  랙 {3}", "抢 {0}  •  第 {1} 局  •  总计 {2}  •  本局 {3}" } },
            { "stats_player_n", new[] { "Người chơi {0}", "Player {0}", "Joueur {0}", "Jugador {0}", "プレイヤー {0}", "플레이어 {0}", "选手 {0}" } },

            // ===== RACK INTRO & NETWORK NOTIFICATIONS =====
            { "rack_intro_skip", new[] { "NHẤN SPACE ĐỂ BỎ QUA  ▶▶", "PRESS SPACE TO SKIP  ▶▶", "APPUYEZ SUR ESPACE POUR PASSER  ▶▶", "PULSA ESPACIO PARA SALTAR  ▶▶", "スペースキーでスキップ  ▶▶", "스페이스바로 넘기기  ▶▶", "按空格键跳过  ▶▶" } },
            { "stats_opponent_left", new[] { "ĐỐI THỦ ĐÃ RỜI TRẬN ĐẤU", "OPPONENT LEFT THE MATCH", "L'ADVERSAIRE A QUITTÉ LE MATCH", "EL OPONENTE ABANDONÓ LA PARTIDA", "対戦相手が退出しました", "상대방이 경기를 떠났습니다", "对手已离开比赛" } },
            { "net_win_disconnect", new[] { "Đối thủ ({0}) đã mất kết nối! Bạn giành chiến thắng trận đấu.", "Opponent ({0}) disconnected! You win the match.", "L'adversaire ({0}) s'est déconnecté ! Victoire.", "¡Oponente ({0}) desconectado! Ganas la partida.", "対戦相手({0})の接続が切断されました！あなたの勝利です。", "상대방({0}) 연결이 끊겼습니다! 당신이 승리했습니다.", "对手（{0}）断开连接！您赢得了比赛。" } },
            { "net_lose_disconnect", new[] { "Bạn đã bị ngắt kết nối khỏi máy chủ! Trận đấu kết thúc.", "You were disconnected from server! Match over.", "Déconnecté du serveur ! Fin du match.", "¡Te has desconectado del servidor! Fin de la partida.", "サーバーから切断されました！試合を終了します。", "서버와의 연결이 끊어졌습니다! 경기가 종료됩니다.", "与服务器断开连接！比赛结束。" } },
            { "online_join_failed", new[] { "Vào phòng thất bại: {0}", "Failed to join room: {0}", "Échec de connexion au salon : {0}", "Fallo al unirse a la sala: {0}", "ルーム参加に失敗しました: {0}", "방 참가 실패: {0}", "加入房间失败：{0}" } },
            { "online_create_failed", new[] { "Tạo phòng thất bại: {0}", "Failed to create room: {0}", "Échec de création du salon : {0}", "Fallo al crear la sala: {0}", "ルーム作成に失敗しました: {0}", "방 생성 실패: {0}", "创建房间失败：{0}" } },
            { "net_not_connected", new[] { "Chưa kết nối tới máy chủ Photon!", "Not connected to Photon server!", "Non connecté au serveur Photon !", "¡No conectado al servidor Photon!", "Photonサーバーに未接続です！", "Photon 서버에 연결되지 않았습니다!", "尚未连接到服务器！" } },
            { "net_room_empty", new[] { "Mã phòng không được để trống!", "Room code cannot be empty!", "Le code du salon ne peut pas être vide !", "¡El código de la sala no puede estar vacío!", "ルームIDを入力してください！", "방 코드를 입력하세요!", "房间代码不能为空！" } },
            { "net_waiting", new[] { "Đang đợi...", "Waiting...", "En attente...", "Esperando...", "待機中...", "대기 중...", "等待中..." } },
            { "net_waiting_opponent", new[] { "Đang đợi đối thủ...", "Waiting for opponent...", "En attente d'un adversaire...", "Esperando oponente...", "対戦相手を待っています...", "상대방 대기 중...", "等待对手..." } },
            { "result_waiting_host", new[] { "CHỜ CHỦ PHÒNG...", "WAITING FOR HOST...", "EN ATTENTE DE L'HÔTE...", "ESPERANDO AL ANFITRIÓN...", "ホスト待機中...", "호스트 대기 중...", "等待房主..." } },
            { "call_safety_opponent", new[] { "Đối thủ chọn đánh An Toàn (Safety)", "Opponent declared Safety", "L'adversaire a joué la sécurité", "El oponente declaró seguridad", "相手がセーフティを選択", "상대가 세이프티를 선언함", "对手声明安全球" } },
            { "call_shot_opponent_format", new[] { "Đối thủ gọi: Bi {0} -> {1}", "Opponent called: Ball {0} -> {1}", "Appel adverse: Bille {0} -> {1}", "Llamada rival: Bola {0} -> {1}", "相手のコール: 球 {0} -> {1}", "상대 콜: 공 {0} -> {1}", "对手指定: {0}号球 -> {1}" } }
        };

        static LocalizationManager()
        {
            LoadSavedLanguage();
        }

        public static void LoadSavedLanguage()
        {
            int savedIndex = PlayerPrefs.GetInt(PrefsIndexKey, -1);
            if (savedIndex >= 0 && savedIndex < LanguageNames.Length)
            {
                currentLanguage = (GameLanguage)savedIndex;
                return;
            }

            string savedCode = PlayerPrefs.GetString(PrefsKey, "vi");
            currentLanguage = CodeToLanguage(savedCode);
        }

        public static void SetLanguage(GameLanguage lang)
        {
            if (currentLanguage == lang) return;

            currentLanguage = lang;
            PlayerPrefs.SetInt(PrefsIndexKey, (int)lang);
            PlayerPrefs.SetString(PrefsKey, LanguageToCode(lang));
            PlayerPrefs.Save();

            Debug.Log($"[LocalizationManager] Ngôn ngữ đã đổi sang: {LanguageNames[(int)lang]}");
            OnLanguageChanged?.Invoke();
        }

        public static void SetLanguageByIndex(int index)
        {
            if (index < 0 || index >= LanguageNames.Length) return;
            SetLanguage((GameLanguage)index);
        }

        public static string Get(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? "";

            if (Dict.TryGetValue(key, out string[] translations))
            {
                int idx = (int)currentLanguage;
                if (idx >= 0 && idx < translations.Length)
                {
                    return translations[idx];
                }
                return translations[0]; // fallback to Vietnamese
            }

            return fallback ?? key;
        }

        public static string L(string key, string fallback = null) => Get(key, fallback);

        public static string Format(string key, params object[] args)
        {
            string template = Get(key, key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        public static string GetPocketName(int pocketIndex)
        {
            string key = "pocket_" + pocketIndex;
            return Get(key, "Lỗ " + (pocketIndex + 1));
        }

        private static GameLanguage CodeToLanguage(string code)
        {
            switch (code?.ToLowerInvariant())
            {
                case "en": return GameLanguage.English;
                case "fr": return GameLanguage.French;
                case "es": return GameLanguage.Spanish;
                case "ja": return GameLanguage.Japanese;
                case "ko": return GameLanguage.Korean;
                case "zh": return GameLanguage.Chinese;
                default: return GameLanguage.Vietnamese;
            }
        }

        private static string LanguageToCode(GameLanguage lang)
        {
            switch (lang)
            {
                case GameLanguage.English: return "en";
                case GameLanguage.French: return "fr";
                case GameLanguage.Spanish: return "es";
                case GameLanguage.Japanese: return "ja";
                case GameLanguage.Korean: return "ko";
                case GameLanguage.Chinese: return "zh";
                default: return "vi";
            }
        }
    }
}
