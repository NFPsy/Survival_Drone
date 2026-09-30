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
  }

});
