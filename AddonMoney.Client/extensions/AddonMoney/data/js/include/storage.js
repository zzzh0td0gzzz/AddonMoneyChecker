var storage=function(){var c="undefined"!=typeof chrome&&void 0!==chrome.storage?"extension":"site";return{getSource:function(){return c},get:function(e,t){if(0<e.length)if("extension"==c)chrome.storage.local.get(e,function(e){t&&"function"==typeof t&&t(e)});else{for(var o={},n=0;n<e.length;n++)o[e[n]]=localStorage.getItem(e[n]);t&&"function"==typeof t&&t(o)}},getObject:function(e,t){if(0<e.length)if("extension"==c)chrome.storage.local.get(e,function(e){t&&"function"==typeof t&&t(e)});else{for(var o={},n=0;n<e.length;n++)o[e[n]]=localStorage.getItem(e[n]),""==o[e[n]]&&(o[e[n]]='""'),o[e[n]]=JSON.parse(o[e[n]]);t&&"function"==typeof t&&t(o)}},set:function(e,t){"extension"==c?chrome.storage.local.set(e,function(e){t&&"function"==typeof t&&t(e)}):($.each(e,function(e,t){localStorage.setItem(e,t)}),t&&"function"==typeof t&&t())},setObject:function(e,t){"extension"==c?chrome.storage.local.set(e,function(e){t&&"function"==typeof t&&t(e)}):($.each(e,function(e,t){localStorage.setItem(e,JSON.stringify(t))}),t&&"function"==typeof t&&t())}}}();