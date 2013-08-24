Array.prototype.unique = function () {
    var hash = {}, unique = [];
    for (var i = 0, l = this.length; i < l; ++i) {
        if (hash.hasOwnProperty(this[i])) {
            continue;
        }
        unique.push(this[i]);
        hash[this[i]] = 1;
    }
    return unique;
};

Array.prototype.none = function (predicate) {
    for (var i = 0, l = this.length; i < l; ++i) {
        if (predicate(this[i])) {
            return false;
        }
    }
    return true;
};

var app = angular.module('iif', []).filter('formatParameters', function () {
    return function (input) {
        return input.map(function (f) {
            return '<span class="type">' + f.type + '</span> ' + f.name;
        }).join('<span class="text-gray">,</span> ');
    };
});

function SelectItem(name, selected) {
    this.name = name;
    this.selected = selected;
};

function IifController($scope, $window) {
    function toSelectItem(value) {
        return new SelectItem(value, false);
    }
    
    function toBool(value) {
        return !!value;
    }
    
    $scope.techs = [
        'MMX',
        'SSE',
        'SSE2',
        'SSE3',
        'SSSE3',
        'SSE4.1',
        'SSE4.2',
        'AVX',
        'AVX2',
        'AVX-512',
        'FMA',
        'Misc',
        'Bit Manipulation',
        'OS',
        'SVML',
        'Other'
    ].map(toSelectItem);

    $scope.types = ['Floating point', 'Integer', 'Inter-Type', 'Mask', 'Other'].map(toSelectItem);
    
    $scope.allCategoriesSelected = true;
    $scope.allTypesSelected = true;
    $scope.allTechsSelected = true;
    $scope.allReturnTypesSelected = true;

    var buffer = $window["FUNCTIONS_DATA"];
    buffer.forEach(function (f) {
        f.isCollapsed = true;
        f.isVisible = true;
    });

    //filtering techs
    var includedTechs = buffer.map(function (i) { return i.tech; })
        .filter(toBool).unique();

    var filtered = [];
    $scope.techs.forEach(function (t) {
        if (includedTechs.indexOf(t.name) != -1) {
            filtered.push(t);
        }
    });
    $scope.techs = filtered;

    //other groups
    $scope.returnTypes = buffer.map(function (i) {
        return i.returnType.replace('unsigned ', '');
    }).filter(toBool).unique().sort().reverse().map(toSelectItem);

    $scope.categories = buffer.map(function (i) {
        return i.category;
    }).filter(toBool).unique().sort().map(toSelectItem);

    $scope.items = buffer;

    $scope.setAllCollapsed = function (value) {
        $scope.items.forEach(function (f) {
            f.isCollapsed = value;
        });
    };

    $scope.resetSearchQuery = function () {
        $scope.searchQuery = '';
        $scope.updateFilter();
    };

    function isSelected(item) {
        return item.selected;
    }


    $scope.updateFilter = function () {
        function getName(item) {
            return item.name;
        }

        $scope.allTypesSelected = $scope.types.none(isSelected);
        $scope.allCategoriesSelected = $scope.categories.none(isSelected);
        $scope.allTechsSelected = $scope.techs.none(isSelected);
        $scope.allReturnTypesSelected = $scope.returnTypes.none(isSelected);

        $scope.items.forEach(function (f) {
            f.isVisible = true;
        });

        if (!$scope.allTechsSelected) {
            var techs = $scope.techs.filter(isSelected).map(getName);
            $scope.items.forEach(function (item) {
                if (techs.indexOf(item.tech) == -1) {
                    item.isVisible = false;
                }
            });
        }

        if (!$scope.allCategoriesSelected) {
            var cats = $scope.categories.filter(isSelected).map(getName);
            $scope.items.forEach(function (item) {
                if (item.isVisible && cats.indexOf(item.category) == -1) {
                    item.isVisible = false;
                }
            });
        }

        if (!$scope.allReturnTypesSelected) {
            var returnTypes = $scope.returnTypes.filter(isSelected).map(getName);
            $scope.items.forEach(function (item) {
                if (item.isVisible && returnTypes.indexOf(item.returnType.replace('unsigned ', '')) == -1) {
                    item.isVisible = false;
                }
            });
        }

        if (!$scope.allTypesSelected) {
            var types = $scope.types.filter(isSelected).map(getName);
            $scope.items.forEach(function (item) {
                if (!item.isVisible) {
                    return;
                }
                item.isVisible = false;
                types.forEach(function (type) {
                    if (type == 'Floating point') {
                        if (item.isFloat) {
                            item.isVisible = true;
                        }
                    } else if (type == 'Integer') {
                        if (item.isInt) {
                            item.isVisible = true;
                        }
                    } else if (type == 'Inter-Type') {
                        if (item.isFloat && item.isInt) {
                            item.isVisible = true;
                        }
                    } else if (type == 'Mask') {
                        if (item.isMask) {
                            item.isVisible = true;
                        }
                    } else if (!item.isFloat && !item.isInt) {
                        item.isVisible = true;
                    }
                });
            });
        }

        if (!!$scope.searchQuery) {
            var queryRegex = new RegExp($scope.searchQuery.replace(/\s+/g, '.*'), 'i');
            $scope.items.filter(isVisible).forEach(function (item) {
                if (!(queryRegex.test(item.name) ||
                    (!!item.instruction && queryRegex.test(item.instruction)) ||
                    (!!item.description && queryRegex.test(item.description)))) {
                    item.isVisible = false;
                }
            });
        }
    };

    function linkSelectAllWithArray(name, items) {
        $scope.$watch(name, function (val) {
            if (!val && items.none(isSelected)) {
                $scope[name] = true;
            } else if (val) {
                items.forEach(function (f) {
                    f.selected = false;
                });
                $scope.updateFilter();
            }
        });
    };

    linkSelectAllWithArray('allCategoriesSelected', $scope.categories);
    linkSelectAllWithArray('allTypesSelected', $scope.types);
    linkSelectAllWithArray('allTechsSelected', $scope.techs);
    linkSelectAllWithArray('allReturnTypesSelected', $scope.returnTypes);
}


IifController.$inject = ['$scope', '$window'];
