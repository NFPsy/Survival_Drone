// 웹(WebGL) 빌드 전용 자바스크립트 함수 모음. C#의 BrowserBridge.cs가 이 함수들을 부른다.
// 유니티 에디터에서는 쓰이지 않고, 웹으로 빌드할 때만 게임에 포함된다.
mergeInto(LibraryManager.library, {

  // 글을 클립보드에 복사한다.
  // 최신 방식(navigator.clipboard)이 막혀 있으면(itch.io처럼 게임이 다른 페이지 안에 끼워진 경우 등)
  // 옛 방식(숨은 입력칸에 글을 넣고 복사 명령)으로 다시 시도한다.
  BrowserBridge_CopyToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);

    var fallbackCopy = function () {
      var area = document.createElement('textarea');
      area.value = text;
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.focus();
      area.select();
      try {
        document.execCommand('copy');
      } catch (e) {
        console.warn('클립보드 복사에 실패했습니다: ' + e);
      }
      document.body.removeChild(area);
    };

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(fallbackCopy);
    } else {
      fallbackCopy();
    }
  },

  // 글을 텍스트 파일로 만들어 브라우저 다운로드를 시작한다.
  // 맨 앞에 BOM(﻿)을 붙여서 윈도우 메모장에서도 한글이 깨지지 않게 한다.
  BrowserBridge_DownloadTextFile: function (fileNamePtr, textPtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var text = UTF8ToString(textPtr);

    var blob = new Blob(['﻿' + text], { type: 'text/plain;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  },

  // 글을 붙여 넣는 입력 창(글상자 + 확인/취소)을 게임 화면 위에 띄운다. (저장 코드 불러오기용)
  // 게임(유니티) 안의 입력칸은 웹에서 붙여넣기가 안 되고, 브라우저 prompt 창은 끼워진 페이지(itch.io)에서 막힐 수 있어서 직접 만든다.
  // 결과는 window.__sdPaste = { state, text } 에 담고, BrowserBridge_PollPasteBox / GetPasteText로 가져간다.
  //   state: 0 = 입력 중, 1 = 확인, 2 = 취소
  BrowserBridge_ShowPasteBox: function (titlePtr) {
    var title = UTF8ToString(titlePtr);
    window.__sdPaste = { state: 0, text: '' };

    var overlay = document.createElement('div');
    overlay.style.cssText = 'position:fixed;left:0;top:0;width:100%;height:100%;background:rgba(0,0,0,0.72);z-index:2147483647;display:flex;align-items:center;justify-content:center;';

    var box = document.createElement('div');
    box.style.cssText = 'background:#101420;color:#e3f5f5;border:1px solid #4d5c75;padding:20px;width:min(560px,90%);font-family:sans-serif;border-radius:6px;box-sizing:border-box;';

    var label = document.createElement('div');
    label.textContent = title;
    label.style.cssText = 'margin-bottom:10px;font-size:15px;line-height:1.5;white-space:pre-wrap;';

    var area = document.createElement('textarea');
    area.placeholder = 'SDSAVE1:...';
    area.style.cssText = 'width:100%;height:150px;box-sizing:border-box;background:#0b0e16;color:#e3f5f5;border:1px solid #4d5c75;font-size:13px;padding:8px;resize:none;';

    var row = document.createElement('div');
    row.style.cssText = 'display:flex;justify-content:flex-end;gap:10px;margin-top:12px;';

    var makeButton = function (text, primary) {
      var button = document.createElement('button');
      button.textContent = text;
      button.style.cssText = 'padding:9px 22px;font-size:15px;border:1px solid #4d5c75;border-radius:4px;cursor:pointer;' +
        (primary ? 'background:#4fd8e8;color:#0b0e14;' : 'background:#161b26;color:#e3f5f5;');
      return button;
    };
    var cancel = makeButton('취소', false);
    var ok = makeButton('확인', true);

    // 전체화면 중이면 전체화면 요소 안에 붙여야 보인다.
    var host = document.fullscreenElement || document.body;
    var finish = function (state) {
      window.__sdPaste.text = area.value;
      window.__sdPaste.state = state;
      if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
    };
    cancel.onclick = function () { finish(2); };
    ok.onclick = function () { finish(1); };

    row.appendChild(cancel);
    row.appendChild(ok);
    box.appendChild(label);
    box.appendChild(area);
    box.appendChild(row);
    overlay.appendChild(box);
    host.appendChild(overlay);
    area.focus();
  },

  // 붙여넣기 입력 창의 상태: 0 = 아직 입력 중(또는 창 없음), 1 = 확인, 2 = 취소.
  BrowserBridge_PollPasteBox: function () {
    return window.__sdPaste ? window.__sdPaste.state : 0;
  },

  // 붙여넣기 입력 창에서 확인을 눌렀을 때의 글. (유니티가 문자열로 받아 가고 메모리를 직접 정리한다)
  BrowserBridge_GetPasteText: function () {
    var text = window.__sdPaste ? window.__sdPaste.text : '';
    window.__sdPaste = null;
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  }

});
