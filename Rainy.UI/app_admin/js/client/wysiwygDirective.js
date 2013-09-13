app.directive('wysiwyg', ['$q', function($q){

    var setupWysiwyg = function (tElement, scope) {
        var parserRules = {
            classes: {
                // for urls
                'internal': 1,
                'url': 1,
                'title': 0
            },
            tags: {
                'strike': {
                    'remove': 0,
                },
                'del': {
                    'remove': 0,
                },
                'blockquote': {
                    rename_tag: 'span'
                },
                'small': {},
                'i': {},
                'a': {},
                'b': {},
                'br': {},
                'li': {},
                'ul': {},
                'h1': {},
                'h2': {},
                'h3': {
                    rename_tag: 'small'
                },
            }
        };

        tElement.wysihtml5({
            html: false,
            link: false,
            image: false,
            color: false,
            parserRules: parserRules,
            stylesheets: ['wysihtml5_style.css'],
            events: {
                change: function () {
                    /* the change event prevents events, like links clicks :( */
                    scope.updateWysiText();
                },
                blur: function() {
                    scope.updateWysiText();
                },
                load: function () {
                    var $doc = $(scope.wysiEditor.composer.doc);
                    $doc.keyup(function (evt) {
                        scope.updateWysiText();
                    });
                    $doc.keydown(function (evt) {
                        scope.updateWysiText();
                    });
                }
            }
        });
        // HACK we modify/remove the bootstrap-wysihtml5 elements from the bar
        $('[data-wysihtml5-command=insertOrderedList]').remove();
        $('[data-wysihtml5-command=Outdent]').remove();
        $('[data-wysihtml5-command=Indent]').remove();

        $('[data-wysihtml5-command=underline]').remove();

        var huge_btn= $('<a data-wysihtml5-command="formatBlock" data-wysihtml5-command-value="h1"><h3>Huge</h3></a>');
        $('[data-wysihtml5-command-value=h1]').replaceWith(huge_btn);

        var large_btn= $('<a data-wysihtml5-command="formatBlock" data-wysihtml5-command-value="h2"><h4>Large</h4></a>');
        $('[data-wysihtml5-command-value=h2]').replaceWith(large_btn);

        var small_btn = $('<a data-wysihtml5-command="formatInline" data-wysihtml5-command-value="small"><small>Small</small></a>');
        $('[data-wysihtml5-command-value=h3]').replaceWith(small_btn);

        var strike_btn = $('<a class="btn" data-wysihtml5-command="formatInline" data-wysihtml5-command-value="strike"><del>Strike</del></a>');
        strike_btn.insertAfter($('[data-wysihtml5-command=italic]'));

        var highlight_btn = $('<a class="btn highlight" data-wysihtml5-command="highlight">Highlight</a>');
        highlight_btn.insertAfter(strike_btn);

        var fixed_btn = $('<a style="font-family: monospace;" class="btn" data-wysihtml5-command="fixedwidth">Fixed</a>');
        fixed_btn.insertAfter(strike_btn);

        //$('[data-wysihtml5-command-value=h3]').replaceWith('<a data-wysihtml5-command=​"formatBlock" data-wysihtml5-command-value=​"h3" href=​"javascript:​;​" unselectable=​"on">Small</a>​');
        //$('[data-wysihtml5-command-value=h3]').replaceWith('<a data-wysihtml5-command=​"formatBlock" data-wysihtml5-command-value=​"h3" href=​"javascript:​;​" unselectable=​"on">Small</a>​');
        $('.wysihtml5-toolbar').find('a').click(function () {
            scope.updateWysiText();
        });
    };

    function setupTitleInput (tElement, scope) {
        // we don't want line breaks in the title so ignore press of enter key
        tElement.keypress(function (e){
            if (e.which === 13) {
                return false;
            }
        });
        // HACK update title on keyup
        var titleUpdateFn = function () {
            scope.$apply(function() {
                var txt = tElement.text();
                if (txt !== '') {
                    scope.selectedNote.title = txt;
                }
            });
        };
        tElement.keyup(titleUpdateFn);
    }

    function setupChangeListeners (tElement, scope) {

        scope.$watch('selectedNote', function (newval, oldval) {
            if (!newval) return;

            if (oldval && newval['note-content'] === oldval['note-content'] &&
                newval.title === oldval.title) {
                // nothing changed
                return;
            }
            if (oldval) {
                scope.onNoteChange(newval);
            }
        }, true);
    }

    return {
        name: 'wysiwyg',
        // priority: 1,
        // terminal: true,

        scope: false,
        //{} = isolate, true = child, false/undefined = no change

        // controller: function ($scope, $element) {},
        // require: '', // Array = multiple requires, ? = optional, ^ = check parent elements
        restrict: 'E', // E = Element, A = Attribute, C = Class, M = Comment
        // template: '',
        templateUrl: 'wysiwyg.html',
        replace: true,
        transclude: false,
        compile: function compile(tElement, tAttrs, transclude) {
            return {
                post: function preLink(scope, tElement, iAttrs, controller){

                    wysihtml5.commands.highlight = {
                        exec: function(composer, command) {
                            return wysihtml5.commands.formatInline.exec(composer, command, 'span', 'highlight', 'highlight');
                        },
                        state: function(composer, command) {
                            return wysihtml5.commands.formatInline.state(composer, command, 'span', 'highlight', 'highlight');
                        }
                    };
                    wysihtml5.commands.fixedwidth = {
                        exec: function(composer, command) {
                            return wysihtml5.commands.formatInline.exec(composer, command, 'pre');
                        },
                        state: function(composer, command) {
                            return wysihtml5.commands.formatInline.state(composer, command, 'pre');
                        }
                    };


                    scope.selectedNote = null;

                    var textarea = tElement.find('textarea');
                    setupWysiwyg(textarea, scope);
                    setupTitleInput(tElement.find('h2'), scope);
                    scope.wysiEditor = textarea.data('wysihtml5').editor;

                    scope.updateWysiText = _.throttle(function () {
                        var newtext = textarea.val();
                        scope.$apply(function () {
                            if (scope.selectedNote)
                                scope.selectedNote['note-content'] = newtext;
                            //console.log('text changed: ' + newtext);
                        });
                    }, 800, { leading: false });

                    scope.setWysiText = function (text) {
                        scope.wysiEditor.setValue(text);
                    };

                    // HACK we sometimes miss any character for any reason?
                    // allow explicit flush (i.e. before sync)
                    scope.flushWysi = function () {
                        var newtext = textarea.val();
                        if (scope.selectedNote)
                            scope.selectedNote['note-content'] = newtext;
                        //console.log('flushed text: ' + newtext);
                    };

                    setupChangeListeners (tElement, scope);
                },
                pre: function postLink(scope, tElement, iAttrs, controller){
                }
            };
        },
    };
}]);
