function findOne(mongoClient, collectionName, model) {
  return function(query, options) {
    options = options || {};

    return new Promise((resolve, reject) => {

      mongoClient
        .collection(collectionName)
        .findOne(query, options)
        .then((result) => {
          if (!result) {
            result = { dbPopulation: true }
          }
          resolve(new model(result));
        })
        .catch(reject)
    });
  }
}

function find(mongoClient, collectionName, model) {
  return function(query, options) {
    options = options || {};

    return new Promise((resolve, reject) => {

      let find = mongoClient
        .collection(collectionName)
        .find(query);

      if (options.sort) {
        find = find.sort(options.sort);
      }

      if (options.skip) {
        find = find.skip(options.skip);
      }

      if (options.limit) {
        find = find.limit(options.limit);
      }

      if (options.project) {
        find = find.project(options.project);
      }

      find.toArray()
        .then((items) => {
          resolve(items.map((item) => {
            return new model(item);
          }));
        })
        .catch(reject)
    });
  }
}

function count(mongoClient, collectionName) {
  return function(query) {
    return new Promise((resolve, reject) => {

      mongoClient
        .collection(collectionName)
        .count(query)
        .then((count) => {
          resolve(count);
        })
        .catch(reject)
    });
  }
}

function insertOne(mongoClient, collectionName) {
  
  return function(data) {
    return new Promise((resolve, reject) => {
      mongoClient
        .collection(collectionName)
        .insertOne(data)
        .then(() => {
          resolve(data);
        })
        .catch(reject)
    });
  }
}

function insertMany(mongoClient, collectionName) {

  return function(data) {
    return new Promise((resolve, reject) => {
      mongoClient
        .collection(collectionName)
        .insertMany(data)
        .then(() => {
          resolve(data);
        })
        .catch(reject)
    });
  }
}

function updateOne(mongoClient, collectionName) {

  return function(query, data) {
    return new Promise((resolve, reject) => {
      mongoClient
        .collection(collectionName)
        .updateOne(query, data)
        .then(() => {
          resolve(data);
        })
        .catch(reject)
    });
  }
}

function updateMany(mongoClient, collectionName) {

  return function(query, modifications) {
    return new Promise((resolve, reject) => {
      mongoClient
        .collection(collectionName)
        .updateMany(query, modifications)
        .then(() => {
          resolve(modifications);
        })
        .catch(reject)
    });
  }
}

function removeOne(mongoClient, collectionName) {
  return function(query) {
    return new Promise((resolve, reject) => {
      mongoClient
        .collection(collectionName)
        .removeOne(query)
        .then(resolve)
        .catch(reject)
    });
  }
}

function removeMany(mongoClient, collectionName) {
  return function(query) {
    return new Promise((resolve, reject) => {
      mongoClient
        .collection(collectionName)
        .removeMany(query)
        .then(resolve)
        .catch(reject)
    });
  }
}

function addIndex(mongoClient, collectionName) {
  return function(query, options) {
    mongoClient
      .collection(collectionName)
      .createIndex(query, options)
      .then(() => {})
      .catch(() => {});
  }
}


module.exports = (mongoClient, collectionName, model) => {
  return {
    findOne: findOne(mongoClient, collectionName, model),
    find: find(mongoClient, collectionName, model),
    count: count(mongoClient, collectionName),
    insertOne: insertOne(mongoClient, collectionName),
    insertMany: insertMany(mongoClient, collectionName),
    updateOne: updateOne(mongoClient, collectionName),
    updateMany: updateMany(mongoClient, collectionName),
    removeOne: removeOne(mongoClient, collectionName),
    removeMany: removeMany(mongoClient, collectionName),
    addIndex: addIndex(mongoClient, collectionName)
  }
};
