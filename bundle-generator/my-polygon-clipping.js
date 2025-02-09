const polygonClipping = require('polygon-clipping')

function helloWord() {
  print( 'Hello World');
}

module.exports.helloWord = function(name) {
  console.log ( 'Hello ' + name);
}

module.exports.polygonClipping = polygonClipping;
